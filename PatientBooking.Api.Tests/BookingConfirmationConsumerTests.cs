using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.BackgroundServices;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

// Regression tests for the 2026-09-10 booking-confirmation consumer review findings:
// unhandled-DB-exception stalls, broad DbUpdateException-as-duplicate handling, legacy
// (pre-NotificationId) messages, and dead-letter/retry-exhaustion behavior under the updated
// at-least-once policy. Real SQL Server, real RabbitMQ (same image as prod), real SMTP via
// smtp4dev - no mocks for infrastructure this project owns or depends on.
[Collection(MessagingCollection.Name)]
public sealed class BookingConfirmationConsumerTests(MessagingTestFixture fixture)
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    // Matches the pre-commit-3d7bf53 shape of BookingConfirmedEvent exactly (verified via
    // `git show 7a72b6a -- .../BookingConfirmedEvent.cs`) - no NotificationId field at all, not
    // just a null one, so deserializing this into today's record leaves NotificationId as
    // Guid.Empty, exactly reproducing what a message from that window looks like today.
    private sealed record LegacyBookingConfirmedEvent(
        int BookingId,
        string BookingNumber,
        string PatientEmail,
        string PatientFullName,
        string ClinicName,
        DateTimeOffset AppointmentStartUtc,
        decimal TotalPrice
    );

    [Fact]
    public async Task EnqueueConfirmationAsync_ValidBooking_DispatchesAndSendsOneEmail()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(90));
        CancellationToken ct = cts.Token;

        (Booking booking, string patientEmail) = await SeedBookingAsync(ct);
        BookingConfirmedEvent evt = new(
            Guid.CreateVersion7(),
            booking.Id,
            booking.BookingNumber,
            patientEmail,
            "Test Patient",
            "Test Clinic",
            booking.AppointmentStartUtc,
            100.00m
        );

        await using (PatientBookingDbContext db = fixture.CreateRawDbContext())
        {
            db.BookingOutboxMessages.Add(
                new BookingOutboxMessage
                {
                    Id = evt.NotificationId,
                    BookingId = booking.Id,
                    Payload = JsonSerializer.Serialize(evt),
                    CreatedAtUtc = Clock.GetUtcNow(),
                }
            );
            await db.SaveChangesAsync(ct);
        }

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var dispatcher = provider.GetRequiredService<BookingOutboxDispatcher>();
        var consumer = provider.GetRequiredService<BookingConfirmationConsumer>();
        await dispatcher.StartAsync(ct);
        await consumer.StartAsync(ct);

        try
        {
            bool dispatched = await WaitUntilAsync(
                async () =>
                {
                    await using PatientBookingDbContext db = fixture.CreateRawDbContext();
                    BookingOutboxMessage? row = await db.BookingOutboxMessages.FindAsync([evt.NotificationId], ct);
                    return row?.DispatchedAtUtc is not null;
                },
                TimeSpan.FromSeconds(30),
                ct
            );
            Assert.True(dispatched, "Outbox row was never marked dispatched.");

            bool sent = await WaitUntilAsync(
                async () => await fixture.GetEmailCountAsync(patientEmail, ct) == 1,
                TimeSpan.FromSeconds(40),
                ct
            );
            int actualCount = await fixture.GetEmailCountAsync(patientEmail, ct);
            Assert.True(sent, $"Expected exactly one email to smtp4dev, got {actualCount}.");

            await using PatientBookingDbContext assertDb = fixture.CreateRawDbContext();
            bool dedupRowExists = await assertDb.SentBookingNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct);
            Assert.True(dedupRowExists);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleDeliveryAsync_DuplicateRedelivery_SendsExactlyOneEmail()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(65));
        CancellationToken ct = cts.Token;

        string email = UniqueEmail();
        BookingConfirmedEvent evt = BuildEvent(email);

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<BookingConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.PublishRawAsync(evt, ct);
            await fixture.PublishRawAsync(evt, ct);

            bool sent = await WaitUntilAsync(async () => await fixture.GetEmailCountAsync(email, ct) >= 1, TimeSpan.FromSeconds(40), ct);
            Assert.True(sent, "Expected at least one email to arrive.");

            // Grace period for a wrongly-duplicated send to show up before asserting the final count.
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            Assert.Equal(1, await fixture.GetEmailCountAsync(email, ct));

            await using PatientBookingDbContext db = fixture.CreateRawDbContext();
            Assert.Equal(1, await db.SentBookingNotifications.CountAsync(n => n.Id == evt.NotificationId, ct));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleDeliveryAsync_SqlServerUnavailableThenRecovers_AllMessagesEventuallyProcessed()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(120));
        CancellationToken ct = cts.Token;

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<BookingConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.StopSqlServerAsync(ct);

            // Fills the consumer's prefetchCount of 5 while the dependency it needs is down. Before
            // the fix, an exception here escaped ReceivedAsync unhandled and the delivery was never
            // acked/rejected - RabbitMQ would then never push a 6th message, and these 5 would stay
            // stuck even after SQL Server came back.
            List<BookingConfirmedEvent> events = Enumerable.Range(0, 5).Select(_ => BuildEvent(UniqueEmail())).ToList();
            foreach (BookingConfirmedEvent evt in events)
            {
                await fixture.PublishRawAsync(evt, ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            await fixture.StartSqlServerAsync(ct);
            await fixture.WaitForSqlServerReadyAsync(ct);

            bool allDelivered = await WaitUntilAsync(
                async () =>
                {
                    foreach (BookingConfirmedEvent evt in events)
                    {
                        if (await fixture.GetEmailCountAsync(evt.PatientEmail, ct) != 1)
                        {
                            return false;
                        }
                    }

                    return true;
                },
                TimeSpan.FromSeconds(60),
                ct
            );

            Assert.True(allDelivered, "Not all 5 messages recovered after SQL Server came back.");
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
            await fixture.StartSqlServerAsync(CancellationToken.None);
            await fixture.WaitForSqlServerReadyAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicatePrimaryKey_ThrowsClassifiableSqlException()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        Guid id = Guid.CreateVersion7();

        await using PatientBookingDbContext db1 = fixture.CreateRawDbContext();
        db1.Add(new SentBookingNotification { Id = id, SentAtUtc = Clock.GetUtcNow() });
        await db1.SaveChangesAsync(ct);

        await using PatientBookingDbContext db2 = fixture.CreateRawDbContext();
        db2.Add(new SentBookingNotification { Id = id, SentAtUtc = Clock.GetUtcNow() });

        DbUpdateException ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync(ct));

        SqlException sqlEx = Assert.IsType<SqlException>(ex.InnerException);
        Assert.True(sqlEx.Number is 2627 or 2601, $"Expected a PK/unique-index violation, got SqlException.Number={sqlEx.Number}.");
    }

    [Fact]
    public async Task HandleDeliveryAsync_LegacyMessageWithoutNotificationId_DedupesAndHandlesTwoBookingsIndependently()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(65));
        CancellationToken ct = cts.Token;

        string email1 = UniqueEmail();
        string email2 = UniqueEmail();
        LegacyBookingConfirmedEvent legacy1 = BuildLegacyEvent(email1);
        LegacyBookingConfirmedEvent legacy2 = BuildLegacyEvent(email2);

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<BookingConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.PublishRawAsync(legacy1, ct);
            await fixture.PublishRawAsync(legacy2, ct);

            bool bothSent = await WaitUntilAsync(
                async () => await fixture.GetEmailCountAsync(email1, ct) == 1 && await fixture.GetEmailCountAsync(email2, ct) == 1,
                TimeSpan.FromSeconds(40),
                ct
            );
            int count1 = await fixture.GetEmailCountAsync(email1, ct);
            int count2 = await fixture.GetEmailCountAsync(email2, ct);
            Assert.True(bothSent, $"Both legacy-shape bookings should get exactly one email each. Got email1={count1}, email2={count2}.");

            // Redeliver booking 1's legacy message again - must dedupe via the derived key, not
            // wrongly skip booking 2 or resend booking 1.
            await fixture.PublishRawAsync(legacy1, ct);
            await Task.Delay(TimeSpan.FromSeconds(3), ct);

            Assert.Equal(1, await fixture.GetEmailCountAsync(email1, ct));
            Assert.Equal(1, await fixture.GetEmailCountAsync(email2, ct));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleDeliveryAsync_MissingPatientEmail_DeadLettersWithoutRetry()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        BookingConfirmedEvent evt = BuildEvent(patientEmail: "");

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<BookingConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            long before = await fixture.GetFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawAsync(evt, ct);

            bool deadLettered = await WaitUntilAsync(
                async () => await fixture.GetFailedQueueMessageCountAsync(ct) > before,
                TimeSpan.FromSeconds(10),
                ct
            );

            Assert.True(deadLettered, "A message with a missing required field should dead-letter immediately, without retry.");
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleDeliveryAsync_PermanentSendFailure_DeadLettersAfterDeliveryLimitExhausted()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        string email = UniqueEmail();
        BookingConfirmedEvent evt = BuildEvent(email);

        // Port 1 is reserved/unassigned - connecting to it fails fast without depending on any
        // externally-closed port on the machine running the tests.
        EmailSettings brokenEmail = new()
        {
            SmtpHost = "127.0.0.1",
            SmtpPort = 1,
            SenderName = fixture.EmailSettings.SenderName,
            SenderEmail = fixture.EmailSettings.SenderEmail,
        };

        await using ServiceProvider provider = fixture.CreateServiceProvider(brokenEmail);
        var consumer = provider.GetRequiredService<BookingConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            long before = await fixture.GetFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawAsync(evt, ct);

            bool deadLettered = await WaitUntilAsync(
                async () => await fixture.GetFailedQueueMessageCountAsync(ct) > before,
                TimeSpan.FromSeconds(45),
                ct
            );

            Assert.True(deadLettered, "A message that always fails to send should dead-letter once the delivery limit is exhausted.");
            Assert.Equal(0, await fixture.GetEmailCountAsync(email, ct));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private static string UniqueEmail() => $"patient-{Guid.NewGuid():N}@patientbooking.test";

    private static BookingConfirmedEvent BuildEvent(string patientEmail) =>
        new(
            Guid.CreateVersion7(),
            Random.Shared.Next(1, int.MaxValue),
            $"BK-{Guid.NewGuid():N}",
            patientEmail,
            "Test Patient",
            "Test Clinic",
            Clock.GetUtcNow().AddDays(1),
            100.00m
        );

    private static LegacyBookingConfirmedEvent BuildLegacyEvent(string patientEmail) =>
        new(
            Random.Shared.Next(1, int.MaxValue),
            $"BK-{Guid.NewGuid():N}",
            patientEmail,
            "Test Patient",
            "Test Clinic",
            Clock.GetUtcNow().AddDays(1),
            100.00m
        );

    private async Task<(Booking Booking, string PatientEmail)> SeedBookingAsync(CancellationToken ct)
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        await using PatientBookingDbContext db = fixture.CreateRawDbContext();

        ApplicationUser user = new()
        {
            UserName = $"patient-{suffix}",
            Email = $"patient-{suffix}@patientbooking.test",
            NormalizedUserName = $"PATIENT-{suffix}".ToUpperInvariant(),
            NormalizedEmail = $"PATIENT-{suffix}@PATIENTBOOKING.TEST".ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        Clinic clinic = new() { Name = $"Test Clinic {suffix}", Address = "1 Test Way" };
        db.Clinics.Add(clinic);
        await db.SaveChangesAsync(ct);

        Patient patient = new() { UserId = user.Id };
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

        Booking booking = new()
        {
            ClinicId = clinic.Id,
            PatientId = patient.Id,
            BookingNumber = $"BK-{suffix}",
            FirstTimeBooking = false,
            AppointmentStartUtc = Clock.GetUtcNow().AddDays(1),
            AppointmentDateUtc = DateOnly.FromDateTime(Clock.GetUtcNow().AddDays(1).UtcDateTime),
            IdempotencyKey = Guid.NewGuid().ToString(),
            CreatedAtUtc = Clock.GetUtcNow(),
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        return (booking, user.Email!);
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout, CancellationToken ct)
    {
        DateTimeOffset deadline = Clock.GetUtcNow() + timeout;
        while (Clock.GetUtcNow() < deadline)
        {
            if (await predicate())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        return await predicate();
    }
}
