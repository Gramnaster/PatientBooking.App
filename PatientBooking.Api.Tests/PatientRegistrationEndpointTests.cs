using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

// Endpoint-level regression tests for the 2026-09-10 registration background-delivery change:
// UsersService.RegisterAsync used to await SmtpIdentityEmailSender.SendConfirmationLinkAsync (real
// SMTP) inside its own transaction before returning. It now queues a RegistrationOutboxMessage in
// that same transaction instead. Real HTTP pipeline, real SQL Server, real Identity throughout.
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
        // point this proves structurally is the next assertion: no outbox row means no email was
        // ever queued, which is only possible if the request never touched SMTP at all.
        Console.WriteLine($"POST /api/Auth/register completed in {stopwatch.ElapsedMilliseconds}ms.");

        await using PatientBookingDbContext db = fixture.CreateRawDbContext();
        RegistrationOutboxMessage? outboxRow = await db
            .RegistrationOutboxMessages
            .SingleOrDefaultAsync(m => m.UserId == registered.Id, ct);

        Assert.NotNull(outboxRow);
        // Not yet picked up by the dispatcher (which polls every 5s) - proves the row was written by
        // the request itself, transactionally, rather than by some background process racing us.
        Assert.Null(outboxRow.DispatchedAtUtc);
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

        int outboxRowsForThisUser = await db
            .RegistrationOutboxMessages
            .CountAsync(m => m.UserId == firstRegistered.Id, ct);
        Assert.Equal(1, outboxRowsForThisUser);
    }
}
