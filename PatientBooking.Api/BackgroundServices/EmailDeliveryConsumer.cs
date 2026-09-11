using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PatientBooking.Api.BackgroundServices;

public sealed class EmailDeliveryConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<EmailDeliveryConsumer> logger
) : BackgroundService
{
    // Delivers every background email (booking/registration confirmation, any future kind) through
    // one shared confirmed-publish / manual-ack / retry / dead-letter / dedup pipeline.
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
                logger.EmailConsumerLoopFailed(ex);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(RetryDelay, clock, stoppingToken).ConfigureAwait(false);
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
            await RabbitMqEmailPublisher.DeclareTopologyAsync(channel, ct);

            // Cap in-flight messages - without a prefetch limit RabbitMQ pushes everything at once.
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5, global: false, cancellationToken: ct);

            AsyncEventingBasicConsumer consumer = new(channel);
            consumer.ReceivedAsync += async (_, ea) => await HandleDeliveryAsync(channel, ea, ct);

            // BasicConsumeAsync returns immediately - block here until shutdown, cancel, or channel loss.
            using CancellationTokenSource idleSignal = new();
            await using CancellationTokenRegistration registration = ct.Register(idleSignal.Cancel);

            channel.ChannelShutdownAsync += async (_, _) =>
            {
                try
                {
                    await idleSignal.CancelAsync();
                }
                catch (ObjectDisposedException)
                {
                    // idleSignal already disposed
                }
            };

            consumer.UnregisteredAsync += async (_, ea) =>
            {
                // Also fires on a broker basic.cancel (e.g. queue deleted) - connection recovery can't
                // fix that, so this is the one case needing a manual restart via the outer retry loop.
                logger.EmailConsumerCancelled(string.Join(',', ea.ConsumerTags));

                try
                {
                    await idleSignal.CancelAsync();
                }
                catch (ObjectDisposedException)
                {
                    // idleSignal already disposed
                }
            };

            await channel.BasicConsumeAsync(
                RabbitMqEmailPublisher.QueueName,
                autoAck: false,
                consumer,
                cancellationToken: ct
            );

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, clock, idleSignal.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Exception expected. Normal shutdown path.
            }
            catch (OperationCanceledException)
            {
                // idleSignal fired from a channel shutdown/consumer cancel - fall through, outer loop retries.
            }
        }
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        EmailEnvelope? evt;
        try
        {
            evt = JsonSerializer.Deserialize<EmailEnvelope>(ea.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.EmailMessageMalformed(ex);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
            return;
        }

        if (
            evt is null || evt.NotificationId == Guid.Empty || string.IsNullOrWhiteSpace(
                evt.Recipient
            ) || string.IsNullOrWhiteSpace(evt.Subject) || string.IsNullOrWhiteSpace(evt.HtmlBody)
        )
        {
            logger.EmailMessageMalformed(null);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
            return;
        }

        if (evt.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= clock.GetUtcNow())
        {
            // Re-checked here (not just at dispatch) so a stale envelope is never sent; plain ack, no
            // dedup row, since it was never sent.
            logger.EmailMessageExpired(evt.NotificationId);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            return;
        }

        try
        {
            // DB and send both live in this try - at prefetchCount 5, unhandled exceptions here would
            // wedge the channel since RabbitMQ won't push more until the backlog acks.
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();

            bool alreadySent = await db.SentEmailNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct);
            if (alreadySent)
            {
                logger.EmailDuplicateSkipped(evt.NotificationId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            var sender = scope.ServiceProvider.GetRequiredService<IEmailTransportSender>();
            await sender.SendAsync(evt.Recipient, evt.Subject, evt.HtmlBody, evt.PlainTextBody, ct);

            try
            {
                db.Add(new SentEmailNotification { Id = evt.NotificationId, SentAtUtc = clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                // Concurrent redelivery beat us to the dedup row - the email already went out either
                // way, so ack rather than retry a send that already happened.
                logger.EmailDuplicateSkipped(evt.NotificationId);
            }

            // Reached on success or a confirmed dup-key race. Other SaveChangesAsync failures retry via
            // the outer catch - no shared transaction, so a rare duplicate beats losing the sent record.
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailMessageHandlingFailed(ex);

            // Delay before RabbitMQ's own delivery-limit retry. Must reject, not nack - nack doesn't
            // count as a delivery attempt and would requeue forever (RabbitMQ 4.3 quorum queues).
            await Task.Delay(SendFailureDelay, clock, ct);

            try
            {
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: true, ct);
            }
            catch (Exception nackEx) when (nackEx is not OperationCanceledException)
            {
                logger.EmailNackFailed(nackEx);
            }
        }
    }

    private async Task NackWithoutRequeueAsync(IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        try
        {
            // requeue:false is its own immediate dead-letter reason (distinct from delivery-limit
            // exhaustion) - a malformed message goes straight to email-delivery.failed, no wasted retries.
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailNackFailed(ex);
        }
    }
}
