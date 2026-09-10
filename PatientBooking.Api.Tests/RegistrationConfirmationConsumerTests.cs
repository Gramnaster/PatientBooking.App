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

// Regression/coverage tests for background registration-email delivery: mirrors
// BookingConfirmationConsumerTests' coverage (confirmed publishing, manual acks, duplicate
// redelivery, SQL-outage recovery, dead-lettering) for the new registration-confirmation queue.
// Real SQL Server, real RabbitMQ (same image as prod), real SMTP via smtp4dev - no mocks for
// infrastructure this project owns or depends on.
[Collection(MessagingCollection.Name)]
public sealed class RegistrationConfirmationConsumerTests(MessagingTestFixture fixture)
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    [Fact]
    public async Task EnqueueConfirmationEmailAsync_ValidRegistration_DispatchesAndSendsOneEmail()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(90));
        CancellationToken ct = cts.Token;

        (string userId, string email) = await SeedUserAsync(ct);
        RegistrationConfirmationEvent evt = new(
            Guid.CreateVersion7(),
            userId,
            email,
            "https://patientbooking.test/api/auth/confirm-email?userId=x&token=y"
        );

        await using (PatientBookingDbContext db = fixture.CreateRawDbContext())
        {
            db.RegistrationOutboxMessages.Add(
                new RegistrationOutboxMessage
                {
                    Id = evt.NotificationId,
                    UserId = userId,
                    Payload = JsonSerializer.Serialize(evt),
                    CreatedAtUtc = Clock.GetUtcNow(),
                }
            );
            await db.SaveChangesAsync(ct);
        }

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var dispatcher = provider.GetRequiredService<RegistrationOutboxDispatcher>();
        var consumer = provider.GetRequiredService<RegistrationConfirmationConsumer>();
        await dispatcher.StartAsync(ct);
        await consumer.StartAsync(ct);

        try
        {
            bool dispatched = await WaitUntilAsync(
                async () =>
                {
                    await using PatientBookingDbContext db = fixture.CreateRawDbContext();
                    RegistrationOutboxMessage? row = await db.RegistrationOutboxMessages.FindAsync([evt.NotificationId], ct);
                    return row?.DispatchedAtUtc is not null;
                },
                TimeSpan.FromSeconds(30),
                ct
            );
            Assert.True(dispatched, "Outbox row was never marked dispatched.");

            bool sent = await WaitUntilAsync(
                async () => await fixture.GetEmailCountAsync(email, ct) == 1,
                TimeSpan.FromSeconds(40),
                ct
            );
            int actualCount = await fixture.GetEmailCountAsync(email, ct);
            Assert.True(sent, $"Expected exactly one email to smtp4dev, got {actualCount}.");

            await using PatientBookingDbContext assertDb = fixture.CreateRawDbContext();
            bool dedupRowExists = await assertDb.SentRegistrationNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct);
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
        RegistrationConfirmationEvent evt = BuildEvent(email);

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<RegistrationConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.PublishRawRegistrationAsync(evt, ct);
            await fixture.PublishRawRegistrationAsync(evt, ct);

            bool sent = await WaitUntilAsync(async () => await fixture.GetEmailCountAsync(email, ct) >= 1, TimeSpan.FromSeconds(40), ct);
            Assert.True(sent, "Expected at least one email to arrive.");

            // Grace period for a wrongly-duplicated send to show up before asserting the final count.
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            Assert.Equal(1, await fixture.GetEmailCountAsync(email, ct));

            await using PatientBookingDbContext db = fixture.CreateRawDbContext();
            Assert.Equal(1, await db.SentRegistrationNotifications.CountAsync(n => n.Id == evt.NotificationId, ct));
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
        var consumer = provider.GetRequiredService<RegistrationConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.StopSqlServerAsync(ct);

            // Fills the consumer's prefetchCount of 5 while the dependency it needs is down - proves
            // HandleDeliveryAsync's scope/dedup lookup being inside the protected try (mirroring
            // BookingConfirmationConsumer's own fix) also holds here.
            List<RegistrationConfirmationEvent> events = Enumerable.Range(0, 5).Select(_ => BuildEvent(UniqueEmail())).ToList();
            foreach (RegistrationConfirmationEvent evt in events)
            {
                await fixture.PublishRawRegistrationAsync(evt, ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            await fixture.StartSqlServerAsync(ct);
            await fixture.WaitForSqlServerReadyAsync(ct);

            bool allDelivered = await WaitUntilAsync(
                async () =>
                {
                    foreach (RegistrationConfirmationEvent evt in events)
                    {
                        if (await fixture.GetEmailCountAsync(evt.Email, ct) != 1)
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
        db1.Add(new SentRegistrationNotification { Id = id, SentAtUtc = Clock.GetUtcNow() });
        await db1.SaveChangesAsync(ct);

        await using PatientBookingDbContext db2 = fixture.CreateRawDbContext();
        db2.Add(new SentRegistrationNotification { Id = id, SentAtUtc = Clock.GetUtcNow() });

        DbUpdateException ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync(ct));

        SqlException sqlEx = Assert.IsType<SqlException>(ex.InnerException);
        Assert.True(sqlEx.Number is 2627 or 2601, $"Expected a PK/unique-index violation, got SqlException.Number={sqlEx.Number}.");
    }

    [Fact]
    public async Task HandleDeliveryAsync_MissingEmail_DeadLettersWithoutRetry()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        RegistrationConfirmationEvent evt = BuildEvent(email: "");

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<RegistrationConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            long before = await fixture.GetRegistrationFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawRegistrationAsync(evt, ct);

            bool deadLettered = await WaitUntilAsync(
                async () => await fixture.GetRegistrationFailedQueueMessageCountAsync(ct) > before,
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
        RegistrationConfirmationEvent evt = BuildEvent(email);

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
        var consumer = provider.GetRequiredService<RegistrationConfirmationConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            long before = await fixture.GetRegistrationFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawRegistrationAsync(evt, ct);

            bool deadLettered = await WaitUntilAsync(
                async () => await fixture.GetRegistrationFailedQueueMessageCountAsync(ct) > before,
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

    private static string UniqueEmail() => $"registrant-{Guid.NewGuid():N}@patientbooking.test";

    private static RegistrationConfirmationEvent BuildEvent(string email) =>
        new(
            Guid.CreateVersion7(),
            Guid.NewGuid().ToString(),
            email,
            "https://patientbooking.test/api/auth/confirm-email?userId=x&token=y"
        );

    private async Task<(string UserId, string Email)> SeedUserAsync(CancellationToken ct)
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        await using PatientBookingDbContext db = fixture.CreateRawDbContext();

        ApplicationUser user = new()
        {
            UserName = $"registrant-{suffix}",
            Email = $"registrant-{suffix}@patientbooking.test",
            NormalizedUserName = $"REGISTRANT-{suffix}".ToUpperInvariant(),
            NormalizedEmail = $"REGISTRANT-{suffix}@PATIENTBOOKING.TEST".ToUpperInvariant(),
            EmailConfirmed = false,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return (user.Id, user.Email!);
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
