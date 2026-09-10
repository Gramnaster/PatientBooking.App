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

// Shared-infrastructure regression tests for background email delivery: one outbox, one queue, one
// consumer for every email kind (booking confirmation, registration confirmation, and any future
// kind) - covers confirmed publishing, manual acks, duplicate redelivery, SQL-outage recovery,
// malformed-message and retry-exhaustion dead-lettering, dead-letter-destination outage/recovery,
// and expiry. Feature-specific composition (what a booking or registration email actually says) is
// covered separately by BookingCancellationEndpointTests/PatientRegistrationEndpointTests - this
// file only exercises the transport pipeline through generic envelopes, so no email type inherits a
// copy of this whole suite just to gain delivery-reliability coverage.
// Real SQL Server, real RabbitMQ (same image as prod), real SMTP via smtp4dev - no mocks for
// infrastructure this project owns or depends on.
[Collection(MessagingCollection.Name)]
public sealed class EmailDeliveryConsumerTests(MessagingTestFixture fixture)
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    [Fact]
    public async Task EnqueueEmailAsync_ValidOutboxRow_DispatchesAndSendsOneEmail()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(90));
        CancellationToken ct = cts.Token;

        string email = UniqueEmail();
        EmailEnvelope evt = BuildEvent(email);

        await using (PatientBookingDbContext db = fixture.CreateRawDbContext())
        {
            db.EmailOutboxMessages.Add(
                new EmailOutboxMessage
                {
                    Id = evt.NotificationId,
                    Recipient = evt.Recipient,
                    Payload = JsonSerializer.Serialize(evt),
                    CreatedAtUtc = Clock.GetUtcNow(),
                }
            );
            await db.SaveChangesAsync(ct);
        }

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var dispatcher = provider.GetRequiredService<EmailOutboxDispatcher>();
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await dispatcher.StartAsync(ct);
        await consumer.StartAsync(ct);

        try
        {
            bool dispatched = await WaitUntilAsync(
                async () =>
                {
                    await using PatientBookingDbContext db = fixture.CreateRawDbContext();
                    EmailOutboxMessage? row = await db.EmailOutboxMessages.FindAsync([evt.NotificationId], ct);
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
            bool dedupRowExists = await assertDb.SentEmailNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct);
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
        EmailEnvelope evt = BuildEvent(email);

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.PublishRawEmailAsync(evt, ct);
            await fixture.PublishRawEmailAsync(evt, ct);

            bool sent = await WaitUntilAsync(async () => await fixture.GetEmailCountAsync(email, ct) >= 1, TimeSpan.FromSeconds(40), ct);
            Assert.True(sent, "Expected at least one email to arrive.");

            // Grace period for a wrongly-duplicated send to show up before asserting the final count.
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            Assert.Equal(1, await fixture.GetEmailCountAsync(email, ct));

            await using PatientBookingDbContext db = fixture.CreateRawDbContext();
            Assert.Equal(1, await db.SentEmailNotifications.CountAsync(n => n.Id == evt.NotificationId, ct));
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
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.StopSqlServerAsync(ct);

            // Fills the consumer's prefetchCount of 5 while the dependency it needs is down. An
            // exception escaping ReceivedAsync unhandled would leave the delivery unacked/unrejected -
            // RabbitMQ would then never push a 6th message, and these 5 would stay stuck even after
            // SQL Server comes back.
            List<EmailEnvelope> events = Enumerable.Range(0, 5).Select(_ => BuildEvent(UniqueEmail())).ToList();
            foreach (EmailEnvelope evt in events)
            {
                await fixture.PublishRawEmailAsync(evt, ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            await fixture.StartSqlServerAsync(ct);
            await fixture.WaitForSqlServerReadyAsync(ct);

            bool allDelivered = await WaitUntilAsync(
                async () =>
                {
                    foreach (EmailEnvelope evt in events)
                    {
                        if (await fixture.GetEmailCountAsync(evt.Recipient, ct) != 1)
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
        db1.Add(new SentEmailNotification { Id = id, SentAtUtc = Clock.GetUtcNow() });
        await db1.SaveChangesAsync(ct);

        await using PatientBookingDbContext db2 = fixture.CreateRawDbContext();
        db2.Add(new SentEmailNotification { Id = id, SentAtUtc = Clock.GetUtcNow() });

        DbUpdateException ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync(ct));

        SqlException sqlEx = Assert.IsType<SqlException>(ex.InnerException);
        Assert.True(sqlEx.Number is 2627 or 2601, $"Expected a PK/unique-index violation, got SqlException.Number={sqlEx.Number}.");
    }

    [Fact]
    public async Task HandleDeliveryAsync_MissingRecipient_DeadLettersWithoutRetry()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        EmailEnvelope evt = BuildEvent(recipient: "");

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            long before = await fixture.GetFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawEmailAsync(evt, ct);

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
        EmailEnvelope evt = BuildEvent(email);

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
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            long before = await fixture.GetFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawEmailAsync(evt, ct);

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

    // RabbitMQ's at-least-once dead lettering retries an unroutable dead-letter delivery on a fixed
    // internal schedule, currently every 3 minutes, and that interval is not exposed as a policy
    // setting: https://www.rabbitmq.com/blog/2022/03/29/at-least-once-dead-lettering. This test's
    // timeouts are sized around that documented interval (with headroom), not an arbitrary pad - see
    // MessagingTestFixture.BlockFailedQueueAsync for why the destination is made unavailable by
    // deleting the queue rather than by a max-length/overflow policy.
    [Fact]
    public async Task HandleDeliveryAsync_DeadLetterDestinationUnavailableThenRecovers_MessageSurvivesAndEventuallyArrives()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));
        CancellationToken ct = cts.Token;

        // Malformed payload dead-letters immediately (requeue:false) rather than waiting out the
        // 3-attempt delivery limit - the outage being tested is the destination's, not the source's.
        EmailEnvelope evt = BuildEvent(recipient: "");

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.BlockFailedQueueAsync(ct);

            long before = await fixture.GetFailedQueueMessageCountAsync(ct);
            await fixture.PublishRawEmailAsync(evt, ct);

            // The destination queue doesn't exist right now, so the dead-letter attempt has no route
            // at all - the message is held by the broker rather than delivered anywhere. The failed
            // queue's count should stay put, not silently lose the message.
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            Assert.Equal(before, await fixture.GetFailedQueueMessageCountAsync(ct));

            await fixture.UnblockFailedQueueAsync(ct);

            bool arrived = await WaitUntilAsync(
                async () => await fixture.GetFailedQueueMessageCountAsync(ct) > before,
                TimeSpan.FromMinutes(4),
                ct
            );

            Assert.True(arrived, "Message should survive the destination outage and eventually reach email-delivery.failed once it recovers.");
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
            await fixture.UnblockFailedQueueAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleDeliveryAsync_ExpiredEnvelope_IsNotSentAndLeavesNoDedupRow()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        string email = UniqueEmail();
        EmailEnvelope evt = new(
            Guid.CreateVersion7(),
            email,
            "Confirm your email",
            "<p>expired</p>",
            PlainTextBody: null,
            Clock.GetUtcNow().AddDays(-2),
            Clock.GetUtcNow().AddDays(-1),
            Kind: "RegistrationConfirmation"
        );

        await using ServiceProvider provider = fixture.CreateServiceProvider();
        var consumer = provider.GetRequiredService<EmailDeliveryConsumer>();
        await consumer.StartAsync(ct);

        try
        {
            await fixture.PublishRawEmailAsync(evt, ct);

            // No positive wait to assert on directly (there is nothing that ever becomes true) - give
            // the consumer a fixed window to have processed the message, then assert the negative.
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            Assert.Equal(0, await fixture.GetEmailCountAsync(email, ct));

            await using PatientBookingDbContext db = fixture.CreateRawDbContext();
            Assert.False(await db.SentEmailNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private static string UniqueEmail() => $"notify-{Guid.NewGuid():N}@patientbooking.test";

    private static EmailEnvelope BuildEvent(string recipient) =>
        new(
            Guid.CreateVersion7(),
            recipient,
            "Test notification",
            "<p>Test body</p>",
            PlainTextBody: null,
            Clock.GetUtcNow(),
            ExpiresAtUtc: null,
            Kind: "Test"
        );

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
