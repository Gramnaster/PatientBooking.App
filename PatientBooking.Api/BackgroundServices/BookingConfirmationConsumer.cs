using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PatientBooking.Api.BackgroundServices;

public sealed class BookingConfirmationConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<BookingConfirmationConsumer> logger
) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SendFailureDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilCancelledOrChannelLostAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.BookingConfirmationConsumerLoopFailed(ex);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ConsumeUntilCancelledOrChannelLostAsync(CancellationToken ct)
    {
        IChannel? channel = await connectionProvider.TryOpenChannelAsync(ct);
        if (channel is null)
        {
            // No broker available right now - outer loop retries after RetryDelay
            return;
        }

        await using (channel.ConfigureAwait(false))
        {
            await RabbitMqBookingEventPublisher.DeclareTopologyAsync(channel, ct);

            // Some in-flight at once than unbounded.
            // RabbitMQ pushes every waiting message to consumer with no prefetch limit set.
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5, global: false, cancellationToken: ct);

            AsyncEventingBasicConsumer consumer = new(channel);
            consumer.ReceivedAsync += async (_, ea) => await HandleDeliveryAsync(channel, ea, ct);

            // BasicConsumeAsync returns once consumer is registered
            // Stay here until shutdown, cancellation, or channel drops out from under us
            using CancellationTokenSource idleSignal = new();
            await using CancellationTokenRegistration registration = ct.Register(idleSignal.Cancel);

            channel.ChannelShutdownAsync += (_, _) =>
            {
                try
                {
                    idleSignal.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // idleSignal already disposed
                }

                return Task.CompletedTask;
            };

            // Fires for both a client-ack'd cancel and a broker-initiated basic.cancel - e.g. the
            // queue was deleted while the connection stayed up. Automatic connection recovery can't
            // fix this (the connection never dropped), so this is the one case that needs a manual
            // restart: redeclare the queue and re-consume via the outer retry loop.
            consumer.UnregisteredAsync += (_, ea) =>
            {
                logger.BookingConfirmationConsumerCancelled(string.Join(',', ea.ConsumerTags));

                try
                {
                    idleSignal.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // idleSignal already disposed
                }

                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(
                RabbitMqBookingEventPublisher.QueueName,
                autoAck: false,
                consumer,
                cancellationToken: ct
            );

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, idleSignal.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Exception expected. Normal shutodwn path.
            }
            catch (OperationCanceledException)
            {
                // idleSignal fired because the channel shut down or the consumer was cancelled -
                // fall through so outer loop retries with a fresh channel after RetryDelay
            }
        }
    }

    // BookingConfirmedEvent gained NotificationId in commit 3d7bf53, ~2 hours after the queue itself
    // was introduced in 7a72b6a. booking-confirmed is a durable quorum queue, so an old-shape message
    // published in that window can still be sitting in a broker's persisted volume. Guid.Empty must
    // never become a shared dedup key - every legacy message would collide on the first one processed -
    // so a legacy message gets a stable key derived from its BookingId instead, which is safe because
    // a booking gets exactly one confirmation event.
    private static readonly Guid LegacyDedupNamespace = new("c46e33d3-9f4e-4f5a-9c0f-3b7a2e6b7d10");

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        BookingConfirmedEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(ea.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.BookingConfirmationMessageMalformed(ex);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
            return;
        }

        if (evt is null || !TryResolveDedupKey(evt, out Guid dedupKey))
        {
            logger.BookingConfirmationMessageMalformed(null);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
            return;
        }

        if (evt.NotificationId == Guid.Empty)
        {
            logger.BookingConfirmationLegacyMessageDetected(evt.BookingId, dedupKey);
        }

        try
        {
            // Scope/DbContext resolution and the dedup lookup live inside this try, same as the
            // send/save below - a DB outage here must not leave the delivery unacked forever. With
            // prefetchCount 5, five unhandled exceptions from ReceivedAsync would wedge the channel
            // even after SQL Server recovers, since RabbitMQ won't push more until those settle.
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();

            bool alreadySent = await db.SentBookingNotifications.AnyAsync(n => n.Id == dedupKey, ct);
            if (alreadySent)
            {
                logger.BookingConfirmationDuplicateSkipped(dedupKey);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            var sender = scope.ServiceProvider.GetRequiredService<IBookingNotificationSender>();
            await sender.SendBookingConfirmationAsync(evt, ct);

            try
            {
                db.Add(new SentBookingNotification { Id = dedupKey, SentAtUtc = clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                // Genuine duplicate-key race: a concurrent redelivery beat us to recording this
                // dedup row. The email already went out (by us or by it) either way, so ack rather
                // than retry a send that already happened.
                logger.BookingConfirmationDuplicateSkipped(dedupKey);
            }

            // Reached on success and on a confirmed duplicate-key race. Any OTHER SaveChangesAsync
            // failure (connection drop, timeout) is NOT caught above, so it propagates to the outer
            // catch and gets retried - SMTP already succeeded at that point, so a retry resends the
            // email. No transaction spans the SMTP send and the DB commit, so exactly-once delivery
            // isn't achievable here; this trades a rare duplicate email for never silently losing the
            // durable "sent" record.
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.BookingConfirmationMessageHandlingFailed(ex);

            // Short delay before the broker-counted redelivery, not a hand-rolled attempt counter -
            // the booking-confirmed-retry-limit policy dead-letters to booking-confirmed.failed once
            // RabbitMQ's own delivery-limit is exceeded. This must be basic.reject, not basic.nack -
            // as of RabbitMQ 4.3, delivery-limit counts on delivery-count, and an explicit nack is
            // treated as an unlimited application-level return that does NOT increment it (so it
            // would requeue forever); only reject (or an actual consumer/channel/connection failure)
            // counts as a real delivery attempt. https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling
            await Task.Delay(SendFailureDelay, ct);

            try
            {
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: true, ct);
            }
            catch (Exception nackEx) when (nackEx is not OperationCanceledException)
            {
                logger.BookingConfirmationNackFailed(nackEx);
            }
        }
    }

    private static bool TryResolveDedupKey(BookingConfirmedEvent evt, out Guid dedupKey)
    {
        if (
            evt.BookingId <= 0 || string.IsNullOrWhiteSpace(evt.PatientEmail) || string.IsNullOrWhiteSpace(
                evt.BookingNumber
            )
        )
        {
            dedupKey = Guid.Empty;
            return false;
        }

        dedupKey = evt.NotificationId != Guid.Empty ? evt.NotificationId : DeriveLegacyDedupKey(evt.BookingId);
        return true;
    }

    private static Guid DeriveLegacyDedupKey(int bookingId)
    {
        string seed = string.Create(CultureInfo.InvariantCulture, $"{LegacyDedupNamespace}:{bookingId}");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash.AsSpan(0, 16));
    }

    private async Task NackWithoutRequeueAsync(IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        try
        {
            // requeue:false is itself an immediate dead-letter reason, distinct from delivery-limit
            // exhaustion - a malformed message goes straight to booking-confirmed.failed, no wasted
            // retries on a payload that will never deserialize.
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.BookingConfirmationNackFailed(ex);
        }
    }
}
