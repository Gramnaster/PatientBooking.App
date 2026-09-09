using System.Text.Json;
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

        if (evt is null)
        {
            logger.BookingConfirmationMessageMalformed(null);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();

        bool alreadySent = await db.SentBookingNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct);
        if (alreadySent)
        {
            logger.BookingConfirmationDuplicateSkipped(evt.NotificationId);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            return;
        }

        try
        {
            var sender = scope.ServiceProvider.GetRequiredService<IBookingNotificationSender>();
            await sender.SendBookingConfirmationAsync(evt, ct);

            db.Add(new SentBookingNotification { Id = evt.NotificationId, SentAtUtc = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race recording the dedup row - a concurrent redelivery beat us to it. The email
            // already went out (by us or by it) either way, so ack rather than retry a send that
            // already happened. This is the crash-window tradeoff: exactly-once isn't possible here,
            // since no transaction spans the SMTP send and the broker/DB ack together.
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
