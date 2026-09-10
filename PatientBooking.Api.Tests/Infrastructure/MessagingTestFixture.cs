using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

// Shared, container-backed infrastructure for the email-delivery consumer regression tests.
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

    // Sql Server container control for the outage/recovery regression test. Stop/Start on the SAME
    // container (not dispose) preserves the port mapping and volume.
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
    // arrange/assert steps (seeding a user, reading dedup rows) without spinning up a full host.
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
        services.AddSingleton<IEmailTransportSender>(sp => sp.GetRequiredService<SmtpIdentityEmailSender>());

        services.Configure<RabbitMqSettings>(o =>
        {
            o.HostName = RabbitMqSettings.HostName;
            o.Port = RabbitMqSettings.Port;
            o.UserName = RabbitMqSettings.UserName;
            o.Password = RabbitMqSettings.Password;
            o.VirtualHost = RabbitMqSettings.VirtualHost;
        });
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IEmailEventPublisher, RabbitMqEmailPublisher>();

        services.AddSingleton<EmailDeliveryConsumer>();
        services.AddSingleton<EmailOutboxDispatcher>();

        return services.BuildServiceProvider();
    }

    // Publishes a raw payload directly to email-delivery, bypassing the outbox - used to drive the
    // consumer's own delivery-handling paths (duplicate redelivery, expiry, malformed fields)
    // independently of EmailOutboxDispatcher.
    public async Task PublishRawEmailAsync<T>(T payload, CancellationToken ct)
    {
        ConnectionFactory factory = BuildConnectionFactory();
        await using IConnection connection = await factory.CreateConnectionAsync(ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqEmailPublisher.DeclareTopologyAsync(channel, ct);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload);
        BasicProperties properties = new() { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: RabbitMqEmailPublisher.QueueName,
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

    // Simulates the dead-letter destination being unavailable. An earlier version of this method
    // applied a max-length:0 / overflow:reject-publish policy directly to email-delivery.failed,
    // on the theory (which the quorum-queues doc page's prose about queue resource limits seems to
    // support) that dead-lettered messages "have to contribute to the queue resource limits...so
    // that the queue can refuse to accept more messages." In practice that did NOT block delivery:
    // the assertion that the failed queue's count stayed put intermittently failed even after
    // confirming (by polling effective_policy_definition) that the policy had genuinely attached.
    // RabbitMQ's own issue tracker documents why: the internal publisher used for at-least-once
    // dead-lettering ignores reject-publish notifications on the target queue, so it can keep
    // enqueueing past the limit - https://github.com/rabbitmq/rabbitmq-server/issues/8495 (opened
    // 2023, still open). That is a genuine, currently-unfixed RabbitMQ server behavior, not a race
    // in this test or a bug in EmailDeliveryConsumer.
    //
    // Deleting the destination queue instead removes the DLX route entirely, which is the mechanism
    // RabbitMQ's own at-least-once dead-lettering documentation describes for an unavailable target:
    // https://www.rabbitmq.com/blog/2022/03/29/at-least-once-dead-lettering - "if there is no route
    // for a dead-lettered message, or one of the target queues does not confirm the message, it will
    // remain in the source queue in a 'neither ready nor unacknowledged' state and be retried by an
    // internal dead-letter consumer process periodically (currently every 3 minutes)." That periodic
    // interval isn't exposed as a policy setting, which is why the recovery test below waits several
    // minutes rather than seconds - a real, evidenced broker characteristic, not padding.
    public async Task BlockFailedQueueAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await rabbitMqManagementApi.DeleteAsync(
            $"/api/queues/%2f/{Uri.EscapeDataString(RabbitMqEmailPublisher.FailedQueueName)}",
            ct
        );
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    // Redeclares (and rebinds) email-delivery.failed - idempotent, same topology DeclareTopologyAsync
    // always creates - so the DLX has a route again and the broker's own retry can deliver whatever
    // it was holding onto.
    public async Task UnblockFailedQueueAsync(CancellationToken ct)
    {
        ConnectionFactory factory = BuildConnectionFactory();
        await using IConnection connection = await factory.CreateConnectionAsync(ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqEmailPublisher.DeclareTopologyAsync(channel, ct);
    }

    public async Task<long> GetFailedQueueMessageCountAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await rabbitMqManagementApi.GetAsync(
            $"/api/queues/%2f/{Uri.EscapeDataString(RabbitMqEmailPublisher.FailedQueueName)}",
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
        await RabbitMqEmailPublisher.DeclareTopologyAsync(channel, CancellationToken.None);
    }

    // Mirrors docs/deployment.md's rabbitmqctl set_policy step via the management HTTP API, since
    // there is no rabbitmqctl binary to shell into from a Testcontainers-managed container.
    private async Task ApplyDeadLetterPolicyAsync()
    {
        Dictionary<string, object> definition = new(StringComparer.Ordinal)
        {
            ["delivery-limit"] = 3,
            ["dead-letter-exchange"] = RabbitMqEmailPublisher.DeadLetterExchangeName,
            ["dead-letter-routing-key"] = RabbitMqEmailPublisher.FailedQueueName,
            ["dead-letter-strategy"] = "at-least-once",
            ["overflow"] = "reject-publish",
        };
        Dictionary<string, object> policy = new(StringComparer.Ordinal)
        {
            ["pattern"] = $"^{RabbitMqEmailPublisher.QueueName}$",
            ["definition"] = definition,
            ["apply-to"] = "queues",
        };

        using StringContent content = new(JsonSerializer.Serialize(policy), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await rabbitMqManagementApi.PutAsync(
            "/api/policies/%2f/email-delivery-retry-limit",
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
