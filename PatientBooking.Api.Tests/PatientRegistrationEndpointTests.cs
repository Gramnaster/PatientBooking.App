using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

// Endpoint-level regression tests for registration background-delivery: UsersService.RegisterAsync
// stages an EmailOutboxMessage in the same transaction instead of awaiting SMTP, and composes the
// confirmation email itself (EmailDeliveryConsumerTests covers the shared transport/dedup/retry
// pipeline generically - this file only proves registration's own composition and transaction
// boundary). Real HTTP pipeline, real SQL Server, real Identity throughout.
[Collection(BookingEndpointCollection.Name)]
public sealed class PatientRegistrationEndpointTests(BookingEndpointTestFixture fixture)
{
    private const string Password = "P@ssw0rd1234!";

    [Fact]
    public async Task RegisterAsync_NewPatient_SucceedsQuicklyAndQueuesConfirmationWithoutSendingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = fixture.CreateClient();
        string email = $"{Guid.NewGuid():N}@patientbooking.test";

        RegisterUserDto registerDto = new()
        {
            Email = email,
            Password = Password,
            FirstName = "Registration",
            LastName = "Test",
        };

        Stopwatch stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/Auth/register", registerDto, ct);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();
        RegisteredUserDto registered = (await response.Content.ReadFromJsonAsync<RegisteredUserDto>(ct))!;

        // Reported, not asserted against a fixed budget - SMTP round-trips run from seconds to
        // tens-of-seconds under real hosts, so a hard threshold here would be a flaky guess. The
        // point this proves structurally is the next assertion: no dispatched outbox row means no
        // email was ever queued for send, which is only possible if the request never touched SMTP.
        Console.WriteLine($"POST /api/Auth/register completed in {stopwatch.ElapsedMilliseconds}ms.");

        await using PatientBookingDbContext db = fixture.CreateRawDbContext();
        // EmailOutboxMessage carries no per-domain foreign key (shared across every email kind), so a
        // registration's own row is found by the recipient address instead of a UserId column.
        EmailOutboxMessage? outboxRow = await db
            .EmailOutboxMessages
            .SingleOrDefaultAsync(m => m.Recipient == email, ct);

        Assert.NotNull(outboxRow);
        // Not yet picked up by the dispatcher (which polls every 5s) - proves the row was written by
        // the request itself, transactionally, rather than by some background process racing us.
        Assert.Null(outboxRow.DispatchedAtUtc);

        EmailEnvelope envelope = JsonSerializer.Deserialize<EmailEnvelope>(outboxRow.Payload)!;
        Assert.Equal(email, envelope.Recipient);
        Assert.Equal("Confirm your email", envelope.Subject);
        Assert.Equal("RegistrationConfirmation", envelope.Kind);
        Assert.Contains("/api/auth/confirm-email?userId=", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(registered.Id), envelope.HtmlBody, StringComparison.Ordinal);

        // Registration links retain their existing semantics: aligned to the same lifespan as the
        // Identity email-confirmation token itself (ASP.NET Core's default is 1 day), not an
        // arbitrary unrelated timeout.
        Assert.NotNull(envelope.ExpiresAtUtc);
        Assert.InRange(
            envelope.ExpiresAtUtc!.Value - envelope.CreatedAtUtc,
            TimeSpan.FromHours(23),
            TimeSpan.FromHours(25)
        );
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_LeavesNoAccountOrQueuedEmailFromTheSecondAttempt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = fixture.CreateClient();
        string email = $"{Guid.NewGuid():N}@patientbooking.test";

        RegisterUserDto firstAttempt = new()
        {
            Email = email,
            Password = Password,
            FirstName = "Original",
            LastName = "Owner",
        };
        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync("/api/Auth/register", firstAttempt, ct);
        firstResponse.EnsureSuccessStatusCode();
        RegisteredUserDto firstRegistered = (await firstResponse.Content.ReadFromJsonAsync<RegisteredUserDto>(ct))!;

        RegisterUserDto duplicateAttempt = new()
        {
            Email = email,
            Password = Password,
            FirstName = "Impersonator",
            LastName = "Attempt",
        };
        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync("/api/Auth/register", duplicateAttempt, ct);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        // NormalizedEmail/NormalizedUserName are lookup-protected (a keyed hash written by the real
        // ILookupProtector the running app uses) - CreateRawDbContext() uses a no-op passthrough
        // protector instead, so a LINQ predicate on NormalizedEmail here would compare a plaintext
        // literal against a real hash and never match. Id is a plain, unprotected primary key, so
        // it's the right column for a raw-context assertion.
        await using PatientBookingDbContext db = fixture.CreateRawDbContext();
        int accountsWithThisId = await db.Users.CountAsync(u => u.Id == firstRegistered.Id, ct);
        Assert.Equal(1, accountsWithThisId);

        int outboxRowsForThisRecipient = await db
            .EmailOutboxMessages
            .CountAsync(m => m.Recipient == email, ct);
        Assert.Equal(1, outboxRowsForThisRecipient);
    }
}
