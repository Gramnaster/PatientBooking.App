using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Testcontainers.MsSql;
using Xunit;

namespace PatientBooking.Api.Tests;

// Verifies the ConsolidateEmailDelivery migration against POPULATED old tables, not an empty
// schema - a fresh-database migration run proves the DDL is syntactically valid but says nothing
// about whether real dedup history actually survives the cutover, which is the entire point of
// docs/deployment.md's "documented maintenance-window cutover, not a destructive drop" design.
// A dedicated, freshly-created SQL Server container is used (not MessagingTestFixture's shared
// one) because this test must control exactly which migrations are applied and in what order -
// something a fixture that is already at the latest schema can't do without disrupting every
// other test sharing it.
public sealed class EmailDeliveryMigrationCompatibilityTests : IAsyncLifetime
{
    // The migration immediately before ConsolidateEmailDelivery - stopping here reproduces the
    // real pre-cutover schema shape (both old outbox tables, both old dedup tables, no shared
    // EmailOutboxMessages/SentEmailNotifications yet).
    private const string PreConsolidationMigration = "20260910130356_AddRegistrationOutboxAndNotificationDedup";

    private static readonly TimeProvider Clock = TimeProvider.System;

    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async ValueTask InitializeAsync() => await sqlContainer.StartAsync();

    public async ValueTask DisposeAsync() => await sqlContainer.DisposeAsync();

    [Fact]
    public async Task MigrateAsync_PopulatedOldOutboxAndDedupTables_CarriesDedupRowsForwardWithoutTouchingOldTables()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using (PatientBookingDbContext preMigrationDb = CreateDbContext())
        {
            IMigrator migrator = ((IInfrastructure<IServiceProvider>)preMigrationDb).Instance.GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PreConsolidationMigration, ct);
        }

        Guid bookingOnlyId = Guid.CreateVersion7();
        Guid registrationOnlyId = Guid.CreateVersion7();

        // Exists as a dedup row in BOTH old tables - an edge case the consolidation migration's
        // WHERE NOT EXISTS guard must not choke on. The second INSERT...SELECT must silently skip
        // it (already carried forward by the first), not throw a duplicate-key violation against
        // SentEmailNotifications' primary key.
        Guid overlappingId = Guid.CreateVersion7();

        int bookingId;
        string userId;

        await using (PatientBookingDbContext seedDb = CreateDbContext())
        {
            ApplicationUser user = new() { UserName = $"migrationcompat-{Guid.NewGuid():N}" };
            Clinic clinic = new() { Name = "Migration Compat Clinic", Address = "1 Test Way" };
            Patient patient = new() { User = user, UserId = user.Id };
            Booking booking = new()
            {
                Patient = patient,
                Clinic = clinic,
                BookingNumber = "M001",
                IdempotencyKey = Guid.NewGuid().ToString(),
                AppointmentStartUtc = Clock.GetUtcNow().AddDays(10),
                AppointmentDateUtc = DateOnly.FromDateTime(Clock.GetUtcNow().AddDays(10).UtcDateTime),
            };
            seedDb.Bookings.Add(booking);
            await seedDb.SaveChangesAsync(ct);
            bookingId = booking.Id;
            userId = user.Id;

            // Both old outbox tables get a real, FK-valid row. The migration has no DDL or DML
            // against either table, so asserting these survive afterward is asserting "still there,
            // byte for byte" - exactly what the runbook's "retained as an inert historical/audit
            // record" claim is supposed to mean in practice, not just in a migration comment.
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO BookingOutboxMessages (Id, BookingId, Payload, CreatedAtUtc, DispatchAttempts)
                VALUES ({Guid.CreateVersion7()}, {bookingId}, {"{}"}, {Clock.GetUtcNow().AddDays(-3)}, {0})
                """,
                ct
            );
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO RegistrationOutboxMessages (Id, UserId, Payload, CreatedAtUtc, DispatchAttempts)
                VALUES ({Guid.CreateVersion7()}, {userId}, {"{}"}, {Clock.GetUtcNow().AddDays(-3)}, {0})
                """,
                ct
            );

            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO SentBookingNotifications (Id, SentAtUtc) VALUES ({bookingOnlyId}, {Clock.GetUtcNow().AddDays(-3)})",
                ct
            );
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO SentBookingNotifications (Id, SentAtUtc) VALUES ({overlappingId}, {Clock.GetUtcNow().AddDays(-2)})",
                ct
            );
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO SentRegistrationNotifications (Id, SentAtUtc) VALUES ({registrationOnlyId}, {Clock.GetUtcNow().AddDays(-2)})",
                ct
            );
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO SentRegistrationNotifications (Id, SentAtUtc) VALUES ({overlappingId}, {Clock.GetUtcNow().AddDays(-1)})",
                ct
            );
        }

        await using PatientBookingDbContext postMigrationDb = CreateDbContext();
        await postMigrationDb.Database.MigrateAsync(ct);

        List<Guid> carriedIds = await postMigrationDb.SentEmailNotifications.Select(n => n.Id).ToListAsync(ct);
        Assert.Contains(bookingOnlyId, carriedIds);
        Assert.Contains(registrationOnlyId, carriedIds);
        Assert.Single(carriedIds, id => id == overlappingId);
        Assert.Equal(3, carriedIds.Count);

        int bookingOutboxCount = await postMigrationDb.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM BookingOutboxMessages WHERE BookingId = {bookingId}")
            .SingleAsync(ct);
        Assert.Equal(1, bookingOutboxCount);

        int registrationOutboxCount = await postMigrationDb.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM RegistrationOutboxMessages WHERE UserId = {userId}")
            .SingleAsync(ct);
        Assert.Equal(1, registrationOutboxCount);

        // Re-runs the migration's own carry-forward SQL a second time, verbatim, simulating a
        // retried or partially-completed migration. docs/deployment.md calls this "safe to re-run" -
        // this proves it rather than trusting the migration's own comment.
        await postMigrationDb.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO SentEmailNotifications (Id, SentAtUtc)
            SELECT b.Id, b.SentAtUtc FROM SentBookingNotifications b
            WHERE NOT EXISTS (SELECT 1 FROM SentEmailNotifications e WHERE e.Id = b.Id);
            """,
            ct
        );
        await postMigrationDb.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO SentEmailNotifications (Id, SentAtUtc)
            SELECT r.Id, r.SentAtUtc FROM SentRegistrationNotifications r
            WHERE NOT EXISTS (SELECT 1 FROM SentEmailNotifications e WHERE e.Id = r.Id);
            """,
            ct
        );
        Assert.Equal(3, await postMigrationDb.SentEmailNotifications.CountAsync(ct));
    }

    private PatientBookingDbContext CreateDbContext()
    {
        DbContextOptions<PatientBookingDbContext> options = new DbContextOptionsBuilder<PatientBookingDbContext>()
            .UseSqlServer(sqlContainer.GetConnectionString())
            .Options;

        return new PatientBookingDbContext(options, new PassthroughPersonalDataProtector(), new PassthroughLookupProtector());
    }
}
