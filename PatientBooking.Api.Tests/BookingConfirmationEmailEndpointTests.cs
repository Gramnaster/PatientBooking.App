using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.DTOs.Booking;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

// Endpoint-level regression tests for booking-confirmation background delivery: BookingServices
// stages an EmailOutboxMessage in the same transaction as the booking itself instead of awaiting
// SMTP, and composes the confirmation email itself using the patient's real (Data-Protection
// decrypted) name (EmailDeliveryConsumerTests covers the shared transport/dedup/retry pipeline
// generically - this file only proves booking's own composition and transaction boundary). Real
// HTTP pipeline, real SQL Server, real Data Protection throughout - nothing here is mocked.
[Collection(BookingEndpointCollection.Name)]
public sealed class BookingConfirmationEmailEndpointTests(BookingEndpointTestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task CreateBookingAsync_ValidBooking_SucceedsQuicklyAndQueuesConfirmationWithReadableEncodedName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Deliberately HTML-sensitive characters, so the assertions below fail if composition ever
        // regresses to writing the raw name (or database ciphertext) straight into the HTML body.
        (HttpClient client, string _, string patientEmail) = await fixture.CreatePatientClientWithEmailAsync(
            "A&B",
            "<C>",
            ct
        );

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/Booking")
        {
            Content = JsonContent.Create(
                new CreateBookingDto
                {
                    ClinicId = fixture.ClinicId,
                    AppointmentStartUtc = RoundDownToHalfHour(DateTimeOffset.UtcNow.AddDays(40)),
                }
            ),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        Stopwatch stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.SendAsync(request, ct);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();
        GetBookingDto created = (await response.Content.ReadFromJsonAsync<GetBookingDto>(JsonOptions, ct))!;

        // Reported, not asserted against a fixed budget - see PatientRegistrationEndpointTests for
        // why. The outbox-row assertion below is what structurally proves no SMTP call happened.
        Console.WriteLine($"POST /api/Booking completed in {stopwatch.ElapsedMilliseconds}ms.");

        await using PatientBookingDbContext db = fixture.CreateRawDbContext();
        EmailOutboxMessage? outboxRow = await db
            .EmailOutboxMessages
            .SingleOrDefaultAsync(m => m.Recipient == patientEmail && m.Kind == "BookingConfirmation", ct);

        Assert.NotNull(outboxRow);
        // Not yet picked up by the dispatcher (which polls every 5s) - proves the row was written by
        // the request itself, transactionally, rather than by some background process racing us.
        Assert.Null(outboxRow.DispatchedAtUtc);

        EmailEnvelope envelope = JsonSerializer.Deserialize<EmailEnvelope>(outboxRow.Payload)!;
        Assert.Equal($"Booking confirmed - {created.BookingNumber}", envelope.Subject);
        Assert.Equal("BookingConfirmation", envelope.Kind);
        // Never expires - purely informational, no token whose validity window matters.
        Assert.Null(envelope.ExpiresAtUtc);

        // Readable name, HTML-encoded - never the raw characters (which would corrupt the markup)
        // and never the encrypted-at-rest ciphertext (which would be unreadable to the recipient).
        Assert.Contains("&lt;C&gt;, A&amp;B", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<C>", envelope.HtmlBody, StringComparison.Ordinal);
    }

    private static DateTimeOffset RoundDownToHalfHour(DateTimeOffset value)
    {
        long ticksPerHalfHour = TimeSpan.FromMinutes(30).Ticks;
        return new DateTimeOffset(value.Ticks - (value.Ticks % ticksPerHalfHour), value.Offset);
    }
}
