using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PatientBooking.Api.Application.DTOs.Booking;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

// Endpoint-level regression tests for the 2026-09-10 booking-cancellation review: GetBookingDto
// omitted the booking's own Id (only bookingNumber, which resets per clinic per day, and
// patientId, which identifies the patient rather than the booking), so a patient had no value they
// could legally pass to DELETE /api/Booking/{id}. Real HTTP pipeline, real SQL Server, real JWT
// auth, and real Data Protection throughout - nothing under test is mocked.
[Collection(BookingEndpointCollection.Name)]
public sealed class BookingCancellationEndpointTests(BookingEndpointTestFixture fixture)
{
    // Matches Program.cs's own AddJsonOptions - the server serializes enums as strings, so the
    // client must be told to expect that too instead of falling back to the numeric default.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task GetMyBookings_NoBearerToken_ReturnsUnauthorized()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/Booking", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancelBookingAsync_OwnEligibleBooking_CancelsAndReleasesSlot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, _) = await fixture.CreatePatientClientAsync("Ana", "Reyes", ct);
        DateTimeOffset appointment = DateTimeOffset.UtcNow.AddDays(30);
        GetBookingDto created = await CreateBookingAsync(client, appointment, ct);

        using HttpResponseMessage cancelResponse = await client.DeleteAsync($"/api/Booking/{created.Id}", ct);

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        // The filtered unique index on (ClinicId, AppointmentStartUtc) only covers active rows, so
        // the cancelled booking's exact slot should now be free for a new booking.
        GetBookingDto rebooked = await CreateBookingAsync(client, appointment, ct);
        Assert.NotEqual(created.Id, rebooked.Id);
    }

    [Fact]
    public async Task CancelBookingAsync_AnotherPatientsBooking_ReturnsNotFoundAndLeavesBookingActive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient ownerClient, _) = await fixture.CreatePatientClientAsync("Ben", "Santos", ct);
        (HttpClient otherClient, _) = await fixture.CreatePatientClientAsync("Carla", "Cruz", ct);
        GetBookingDto created = await CreateBookingAsync(ownerClient, DateTimeOffset.UtcNow.AddDays(31), ct);

        using HttpResponseMessage cancelResponse = await otherClient.DeleteAsync($"/api/Booking/{created.Id}", ct);

        // Missing and not-owned both return NotFound, by design - the caller can't distinguish them.
        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);

        using HttpResponseMessage getResponse = await ownerClient.GetAsync($"/api/Booking/{created.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task CancelBookingAsync_InsideCutoffWindow_ReturnsConflict()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, _) = await fixture.CreatePatientClientAsync("Dado", "Lim", ct);
        GetBookingDto created = await CreateBookingAsync(client, DateTimeOffset.UtcNow.AddHours(3), ct);

        using HttpResponseMessage cancelResponse = await client.DeleteAsync($"/api/Booking/{created.Id}", ct);

        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyCancelled_ReturnsNotFoundOnSecondAttempt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, _) = await fixture.CreatePatientClientAsync("Erica", "Tan", ct);
        GetBookingDto created = await CreateBookingAsync(client, DateTimeOffset.UtcNow.AddDays(32), ct);

        using HttpResponseMessage firstCancel = await client.DeleteAsync($"/api/Booking/{created.Id}", ct);
        using HttpResponseMessage secondCancel = await client.DeleteAsync($"/api/Booking/{created.Id}", ct);

        Assert.Equal(HttpStatusCode.NoContent, firstCancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, secondCancel.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_AfterCreate_ExposesBookingIdDistinctFromBookingNumberAndPatientId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, _) = await fixture.CreatePatientClientAsync("Faye", "Cortez", ct);
        GetBookingDto created = await CreateBookingAsync(client, DateTimeOffset.UtcNow.AddDays(33), ct);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/Booking", ct);
        listResponse.EnsureSuccessStatusCode();
        List<GetBookingDto> bookings = (await listResponse.Content.ReadFromJsonAsync<List<GetBookingDto>>(JsonOptions, ct))!;

        GetBookingDto listed = Assert.Single(bookings, b => b.Id == created.Id);
        Assert.NotEqual(0, listed.Id);
        Assert.NotEqual(listed.Id, listed.PatientId);
        Assert.StartsWith("A", listed.BookingNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBookingAsync_EncryptedPatientName_ReturnsDecryptedFullNameWithGenuinelyEncryptedStorage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, string userId) = await fixture.CreatePatientClientAsync("Jane", "Dela Cruz", ct);
        GetBookingDto created = await CreateBookingAsync(client, DateTimeOffset.UtcNow.AddDays(34), ct);

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/Booking/{created.Id}", ct);
        getResponse.EnsureSuccessStatusCode();
        GetBookingDto fetched = (await getResponse.Content.ReadFromJsonAsync<GetBookingDto>(JsonOptions, ct))!;
        Assert.Equal("Dela Cruz, Jane", fetched.PatientFullName);

        await using PatientBookingDbContext rawDb = fixture.CreateRawDbContext();
        string storedFirstName = await rawDb.Database
            .SqlQuery<string>($"SELECT FirstName AS Value FROM AspNetUsers WHERE Id = {userId}")
            .SingleAsync(ct);
        Assert.NotEqual("Jane", storedFirstName);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        IPersonalDataProtector protector = scope.ServiceProvider.GetRequiredService<IPersonalDataProtector>();
        Assert.Equal("Jane", protector.Unprotect(storedFirstName));
    }

    private async Task<GetBookingDto> CreateBookingAsync(HttpClient client, DateTimeOffset appointmentStartUtc, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/Booking")
        {
            Content = JsonContent.Create(
                new CreateBookingDto { ClinicId = fixture.ClinicId, AppointmentStartUtc = RoundDownToHalfHour(appointmentStartUtc) }
            ),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using HttpResponseMessage response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"CreateBookingAsync failed with {response.StatusCode}: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<GetBookingDto>(JsonOptions, ct))!;
    }

    // CreateBookingDto requires AppointmentStartUtc on a half-hour boundary.
    private static DateTimeOffset RoundDownToHalfHour(DateTimeOffset value)
    {
        long ticksPerHalfHour = TimeSpan.FromMinutes(30).Ticks;
        return new DateTimeOffset(value.Ticks - (value.Ticks % ticksPerHalfHour), value.Offset);
    }
}
