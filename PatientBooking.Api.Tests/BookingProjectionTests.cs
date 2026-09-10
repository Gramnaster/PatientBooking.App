using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Services;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Domain.Security;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

[Collection(MessagingCollection.Name)]
public sealed class BookingProjectionTests(MessagingTestFixture fixture)
{
    [Fact]
    public async Task GetByIdForClinicAsync_EncryptedPatientNames_ReturnsDecryptedFullName()
    {
        // Arrange: use real protection and a separate EF model cache from the passthrough fixture.
        await using var fixtureDb = fixture.CreateRawDbContext();
        var options = new DbContextOptionsBuilder<PatientBookingDbContext>()
            .UseSqlServer(fixtureDb.Database.GetConnectionString())
            .EnableServiceProviderCaching(false)
            .Options;
        var protector = new PersonalDataProtector(new EphemeralDataProtectionProvider());
        await using var db = new PatientBookingDbContext(options, protector, new PassthroughLookupProtector());
        var user = new ApplicationUser
        {
            UserName = $"projection-{Guid.NewGuid():N}",
            FirstName = "Jane",
            LastName = "Dela Cruz",
        };
        var clinic = new Clinic { Name = "Projection Clinic", Address = "1 Test Way" };
        var patient = new Patient { User = user, UserId = user.Id };
        var booking = new Booking
        {
            Patient = patient,
            Clinic = clinic,
            BookingNumber = "A001",
            IdempotencyKey = Guid.NewGuid().ToString(),
            AppointmentStartUtc = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
            AppointmentDateUtc = new DateOnly(2026, 9, 15),
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        string storedName = await db.Database
            .SqlQuery<string>($"SELECT FirstName AS Value FROM AspNetUsers WHERE Id = {user.Id}")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(user.FirstName, storedName);
        Assert.Equal(user.FirstName, protector.Unprotect(storedName));
        db.ChangeTracker.Clear();

        // Act: exercise the same projection used to populate booking outbox messages.
        IBookingService service = new BookingServices(db, new HttpContextAccessor(), TimeProvider.System);
        var result = await service.GetByIdForClinicAsync(clinic.Id, booking.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Dela Cruz, Jane", result.Value!.PatientFullName);
    }
}
