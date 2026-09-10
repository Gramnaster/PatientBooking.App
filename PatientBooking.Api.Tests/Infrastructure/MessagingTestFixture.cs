using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Application.Services;
using PatientBooking.Api.BackgroundServices;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Domain.Security;
using RabbitMQ.Client;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace PatientBooking.Api.Tests.Infrastructure;

// Shared, container-backed infrastructure for the booking-confirmation consumer regression tests.
// One SQL Server + one RabbitMQ (same image as prod/dev) + one smtp4dev container for the whole
// collection - tests run sequentially (xUnit's default within a single collection), so each test
// gets an exclusive ServiceProvider/consumer instance for its duration rather than fighting over a
// long-lived shared consumer bound to the same queue.
public sealed class MessagingTestFixture : IAsyncLifetime
{
    // Non-"guest" username sidesteps RabbitMQ's loopback_users restriction on the "guest" account,
    // which otherwise rejects AMQP logins that don't originate from the broker's own loopback -
    // verified empirically against a real container before writing this, not assumed.
    private const string RabbitMqUser = "testuser";
    private const string RabbitMqPassword = "testpass";
    private const int RabbitMqManagementPort = 15672;
    private const int SmtpPort = 25;
    private const int SmtpWebPort = 80;

    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // Pinned to match docker-compose.yml/docker/dev.compose.yaml - the floating "4-management-alpine"
    // tag moved from 4.3.4 to 4.3.5 mid-development of this suite, so testing against it is testing
    // against a moving target. Bump this and the two Compose files together, deliberately.
    private readonly RabbitMqContainer rabbitContainer = new RabbitMqBuilder("rabbitmq:4.3.4-management-alpine")
        .WithUsername(RabbitMqUser)
        .WithPassword(RabbitMqPassword)
        .WithPortBinding(RabbitMqManagementPort, true)
        .Build();

    private readonly IContainer smtpContainer = new ContainerBuilder("rnwood/smtp4dev")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(SmtpWebPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(SmtpWebPort).ForPath("/api/messages")))
        .Build();

    private readonly HttpClient smtpApi = new();
    private readonly HttpClient rabbitMqManagementApi = new();

    public RabbitMqSettings RabbitMqSettings { get; private set; } = null!;
    public EmailSettings EmailSettings { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(sqlContainer.StartAsync(), rabbitContainer.StartAsync(), smtpContainer.StartAsync());

        RabbitMqSettings = new RabbitMqSettings
        {
            HostName = rabbitContainer.Hostname,
            Port = rabbitContainer.GetMappedPublicPort(5672),
            UserName = RabbitMqUser,
            Password = RabbitMqPassword,
            VirtualHost = "/",
        };

        EmailSettings = new EmailSettings
        {
            SmtpHost = smtpContainer.Hostname,
            SmtpPort = smtpContainer.GetMappedPublicPort(SmtpPort),
            SenderName = "PatientBooking Tests",
            SenderEmail = "noreply@patientbooking.test",
        };

        smtpApi.BaseAddress = new Uri($"http://{smtpContainer.Hostname}:{smtpContainer.GetMappedPublicPort(SmtpWebPort)}");

        rabbitMqManagementApi.BaseAddress =
            new Uri($"http://{rabbitContainer.Hostname}:{rabbitContainer.GetMappedPublicPort(RabbitMqManagementPort)}");
        rabbitMqManagementApi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{RabbitMqUser}:{RabbitMqPassword}"))
        );

        await MigrateDatabaseAsync();
        await DeclareTopologyAsync();
        await ApplyDeadLetterPolicyAsync();
    }

    public async ValueTask DisposeAsync()
    {
        smtpApi.Dispose();
        rabbitMqManagementApi.Dispose();

        await Task.WhenAll(sqlContainer.DisposeAsync().AsTask(), rabbitContainer.DisposeAsync().AsTask(), smtpContainer.DisposeAsync().AsTask());
    }

    // Sql Server container control for the outage/recovery regression test (finding #1). Stop/Start
    // on the SAME container (not dispose) preserves the port mapping and volume.
    public Task StopSqlServerAsync(CancellationToken ct) => sqlContainer.StopAsync(ct);

    public Task StartSqlServerAsync(CancellationToken ct) => sqlContainer.StartAsync(ct);

    public async Task WaitForSqlServerReadyAsync(CancellationToken ct)
    {
        TimeSpan deadline = TimeSpan.FromSeconds(60);
        DateTimeOffset start = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - start < deadline)
        {
            try
            {
                await using PatientBookingDbContext db = CreateRawDbContext();
                _ = await db.Database.CanConnectAsync(ct);
                return;
            }
            catch when (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
        }

        throw new TimeoutException("SQL Server did not become reachable again within the deadline.");
    }

    // A fresh DbContext instance, independent of any test's ServiceProvider - used for direct
    // arrange/assert steps (seeding a Booking, reading dedup rows) without spinning up a full host.
    public PatientBookingDbContext CreateRawDbContext()
    {
        DbContextOptions<PatientBookingDbContext> options = new DbContextOptionsBuilder<PatientBookingDbContext>()
            .UseSqlServer(sqlContainer.GetConnectionString())
            .Options;

        return new PatientBookingDbContext(options, new PassthroughPersonalDataProtector(), new PassthroughLookupProtector());
    }

    // A fully wired ServiceProvider mirroring Program.cs's messaging-relevant registrations.
    // emailOverride lets one test (permanent-send-failure/retry-exhaustion) point SMTP at an
    // unreachable host without disturbing the shared EmailSettings other tests rely on.
    public ServiceProvider CreateServiceProvider(EmailSettings? emailOverride = null)
    {
        EmailSettings effectiveEmail = emailOverride ?? EmailSettings;
        ServiceCollection services = new();

        services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<PatientBookingDbContext>(o => o.UseSqlServer(sqlContainer.GetConnectionString()));
        services.AddSingleton<IPersonalDataProtector, PassthroughPersonalDataProtector>();
        services.AddSingleton<ILookupProtector, PassthroughLookupProtector>();

        // SmtpPort is init-only, so Configure<T>'s mutate-the-default-instance pattern can't set it -
        // register the already-built instance directly instead.
        services.AddSingleton<IOptions<EmailSettings>>(Options.Create(effectiveEmail));
        services.AddSingleton<SmtpIdentityEmailSender>();
        services.AddSingleton<IBookingNotificationSender>(sp => sp.GetRequiredService<SmtpIdentityEmailSender>());

        services.Configure<RabbitMqSettings>(o =>
        {
            o.HostName = RabbitMqSettings.HostName;
            o.Port = RabbitMqSettings.Port;
            o.UserName = RabbitMqSettings.UserName;
            o.Password = RabbitMqSettings.Password;
            o.VirtualHost = RabbitMqSettings.VirtualHost;
        });
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IBookingEventPublisher, RabbitMqBookingEventPublisher>();

        services.AddSingleton<BookingConfirmationConsumer>();
        services.AddSingleton<BookingOutboxDispatcher>();

        return services.BuildServiceProvider();
    }

    // Publishes a raw payload directly to booking-confirmed, bypassing the outbox - used to drive
    // the consumer's own delivery-handling paths (duplicate redelivery, legacy shape, malformed
    // fields) independently of BookingOutboxDispatcher.
    public async Task PublishRawAsync<T>(T payload, CancellationToken ct)
    {
        ConnectionFactory factory = BuildConnectionFactory();
        await using IConnection connection = await factory.CreateConnectionAsync(ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqBookingEventPublisher.DeclareTopologyAsync(channel, ct);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload);
        BasicProperties properties = new() { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: RabbitMqBookingEventPublisher.QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct
        );
    }

    // smtp4dev's deliveredTo query parameter matches by substring, not exact equality - verified
    // empirically (filtering by one full recipient address also returned an unrelated recipient that
    // merely shared the same domain). Every test in this suite uses addresses under the same
    // @patientbooking.test domain, so relying on the server-side filter would make later tests see
    // earlier tests' messages as false positives. It's kept as a coarse pre-filter (never returns
    // fewer than the exact matches) and the real count is exact-matched client-side.
    public async Task<int> GetEmailCountAsync(string recipientEmail, CancellationToken ct)
    {
        using HttpResponseMessage response = await smtpApi.GetAsync(
            $"/api/messages?deliveredTo={Uri.EscapeDataString(recipientEmail)}&pageSize=500",
            ct
        );
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        int count = 0;
        foreach (JsonElement message in document.RootElement.GetProperty("results").EnumerateArray())
        {
            if (string.Equals(message.GetProperty("deliveredTo").GetString(), recipientEmail, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    // Simulates the dead-letter destination being unavailable, per RabbitMQ's own recommended way to
    // exercise at-least-once dead-lettering: https://www.rabbitmq.com/docs/quorum-queues#dead-lettering
    // says the target can "push back" by rejecting an enqueue (e.g. reaching max-length with
    // overflow: reject-publish), which is exactly what a 0-length policy on the queue itself forces
    // for every publish attempt, including internal dead-letter routing. There's no way to take just
    // booking-confirmed.failed offline via container control - it lives on the same broker process as
    // booking-confirmed.
    private const string FailedQueueOutagePolicyName = "booking-confirmed-failed-outage-test";

    public async Task BlockFailedQueueAsync(CancellationToken ct)
    {
        Dictionary<string, object> definition = new(StringComparer.Ordinal)
        {
            ["max-length"] = 0,
            ["overflow"] = "reject-publish",
        };
        Dictionary<string, object> policy = new(StringComparer.Ordinal)
        {
            ["pattern"] = $"^{Regex.Escape(RabbitMqBookingEventPublisher.FailedQueueName)}$",
            ["definition"] = definition,
            ["apply-to"] = "queues",
        };

        using StringContent content = new(JsonSerializer.Serialize(policy), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await rabbitMqManagementApi.PutAsync(
            $"/api/policies/%2f/{FailedQueueOutagePolicyName}",
            content,
            ct
        );
        response.EnsureSuccessStatusCode();
    }

    public async Task UnblockFailedQueueAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await rabbitMqManagementApi.DeleteAsync(
            $"/api/policies/%2f/{FailedQueueOutagePolicyName}",
            ct
        );

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<long> GetFailedQueueMessageCountAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await rabbitMqManagementApi.GetAsync(
            $"/api/queues/%2f/{Uri.EscapeDataString(RabbitMqBookingEventPublisher.FailedQueueName)}",
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.TryGetProperty("messages", out JsonElement messages) ? messages.GetInt64() : 0;
    }

    private async Task MigrateDatabaseAsync()
    {
        await using PatientBookingDbContext db = CreateRawDbContext();
        await db.Database.MigrateAsync();
    }

    private async Task DeclareTopologyAsync()
    {
        ConnectionFactory factory = BuildConnectionFactory();
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();
        await RabbitMqBookingEventPublisher.DeclareTopologyAsync(channel, CancellationToken.None);
    }

    // Mirrors docs/deployment.md's rabbitmqctl set_policy step via the management HTTP API, since
    // there is no rabbitmqctl binary to shell into from a Testcontainers-managed container.
    private async Task ApplyDeadLetterPolicyAsync()
    {
        Dictionary<string, object> definition = new(StringComparer.Ordinal)
        {
            ["delivery-limit"] = 3,
            ["dead-letter-exchange"] = RabbitMqBookingEventPublisher.DeadLetterExchangeName,
            ["dead-letter-routing-key"] = RabbitMqBookingEventPublisher.FailedQueueName,
            ["dead-letter-strategy"] = "at-least-once",
            ["overflow"] = "reject-publish",
        };
        Dictionary<string, object> policy = new(StringComparer.Ordinal)
        {
            ["pattern"] = "^booking-confirmed$",
            ["definition"] = definition,
            ["apply-to"] = "queues",
        };

        using StringContent content = new(JsonSerializer.Serialize(policy), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await rabbitMqManagementApi.PutAsync(
            "/api/policies/%2f/booking-confirmed-retry-limit",
            content
        );
        response.EnsureSuccessStatusCode();
    }

    private ConnectionFactory BuildConnectionFactory() =>
        new()
        {
            HostName = RabbitMqSettings.HostName,
            Port = RabbitMqSettings.Port,
            UserName = RabbitMqSettings.UserName,
            Password = RabbitMqSettings.Password,
            VirtualHost = RabbitMqSettings.VirtualHost,
        };
}

// Tests never inspect encrypted/lookup-hashed columns, so a no-op stand-in avoids requiring
// ASP.NET Core Data Protection key storage in an ephemeral test host.
internal sealed class PassthroughPersonalDataProtector : IPersonalDataProtector
{
    public string? Protect(string? data) => data;

    public string? Unprotect(string? data) => data;
}

internal sealed class PassthroughLookupProtector : ILookupProtector
{
    public string? Protect(string keyId, string? data) => data;

    public string? Unprotect(string keyId, string? data) => data;
}
