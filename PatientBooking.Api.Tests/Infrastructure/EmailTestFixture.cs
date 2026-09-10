using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientBooking.Api.Application.Services;
using PatientBooking.Api.Common.Models.Config;
using Xunit;

namespace PatientBooking.Api.Tests.Infrastructure;

// Real SMTP via smtp4dev (same image as MessagingTestFixture/local dev) - no mock IEmailSender, so
// an encoding or formatting bug in the actual rendered body fails these tests the same way it would
// fail in production. No SQL Server or RabbitMQ container: these tests only exercise
// SmtpIdentityEmailSender directly, never the booking/outbox/consumer pipeline.
public sealed class EmailTestFixture : IAsyncLifetime
{
    private const int SmtpPort = 25;
    private const int SmtpWebPort = 80;

    private readonly IContainer smtpContainer = new ContainerBuilder("rnwood/smtp4dev")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(SmtpWebPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(SmtpWebPort).ForPath("/api/messages")))
        .Build();

    private readonly HttpClient smtpApi = new();

    public SmtpIdentityEmailSender Sender { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await smtpContainer.StartAsync();

        EmailSettings settings = new()
        {
            SmtpHost = smtpContainer.Hostname,
            SmtpPort = smtpContainer.GetMappedPublicPort(SmtpPort),
            SenderName = "PatientBooking Tests",
            SenderEmail = "noreply@patientbooking.test",
        };

        smtpApi.BaseAddress = new Uri($"http://{smtpContainer.Hostname}:{smtpContainer.GetMappedPublicPort(SmtpWebPort)}");

        Sender = new SmtpIdentityEmailSender(
            Options.Create(settings),
            LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true)).CreateLogger<SmtpIdentityEmailSender>()
        );
    }

    public async ValueTask DisposeAsync()
    {
        smtpApi.Dispose();
        await smtpContainer.DisposeAsync();
    }

    // Polls until a message addressed to recipientEmail arrives, then returns its HTML body exactly
    // as smtp4dev decoded it from the MIME part - not our own re-parsing of a raw multipart source.
    public async Task<string> GetLatestHtmlBodyAsync(string recipientEmail, CancellationToken ct)
    {
        TimeSpan deadline = TimeSpan.FromSeconds(30);
        DateTimeOffset start = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - start < deadline)
        {
            using HttpResponseMessage listResponse = await smtpApi.GetAsync(
                $"/api/messages?deliveredTo={Uri.EscapeDataString(recipientEmail)}&pageSize=50",
                ct
            );
            listResponse.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(ct));

            foreach (JsonElement message in document.RootElement.GetProperty("results").EnumerateArray())
            {
                if (!string.Equals(message.GetProperty("deliveredTo").GetString(), recipientEmail, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string id = message.GetProperty("id").GetString()!;
                using HttpResponseMessage htmlResponse = await smtpApi.GetAsync($"/api/messages/{id}/html", ct);
                htmlResponse.EnsureSuccessStatusCode();
                return await htmlResponse.Content.ReadAsStringAsync(ct);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }

        throw new TimeoutException($"No message delivered to {recipientEmail} within the deadline.");
    }
}
