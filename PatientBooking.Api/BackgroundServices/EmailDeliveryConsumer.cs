using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PatientBooking.Api.BackgroundServices;

// Delivers every background-sent email (booking confirmation, registration confirmation, and any
// future kind) through one shared confirmed-publish / manual-ack / retry / dead-letter / dedup
// pipeline. This consumer never inspects Kind to rebuild per-feature logic - it only sends the
// envelope's already-composed Subject/HtmlBody through the actual transport sender.
public sealed class EmailDeliveryConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<EmailDeliveryConsumer> logger
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
                logger.EmailConsumerLoopFailed(ex);
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
            await RabbitMqEmailPublisher.DeclareTopologyAsync(channel, ct);

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
                logger.EmailConsumerCancelled(string.Join(',', ea.ConsumerTags));

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
                RabbitMqEmailPublisher.QueueName,
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
                // Exception expected. Normal shutdown path.
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

        // A message can sit in the queue for a while (broker/consumer downtime) - re-check expiry at
        // send time too, not just at dispatch time, so a stale envelope is never sent nor recorded as
        // sent. Dropped via a plain ack (not a dedup row): there is nothing to deduplicate against
        // since it was never sent.
        if (evt.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= clock.GetUtcNow())
        {
            logger.EmailMessageExpired(evt.NotificationId);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            return;
        }

        try
        {
            // Scope/DbContext resolution and the dedup lookup live inside this try, same as the
            // send/save below - a DB outage here must not leave the delivery unacked forever. With
            // prefetchCount 5, five unhandled exceptions from ReceivedAsync would wedge the channel
            // even after SQL Server recovers, since RabbitMQ won't push more until those settle.
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
                // Genuine duplicate-key race: a concurrent redelivery beat us to recording this
                // dedup row. The email already went out (by us or by it) either way, so ack rather
                // than retry a send that already happened.
                logger.EmailDuplicateSkipped(evt.NotificationId);
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
            logger.EmailMessageHandlingFailed(ex);

            // Short delay before the broker-counted redelivery, not a hand-rolled attempt counter -
            // the email-delivery-retry-limit policy dead-letters to email-delivery.failed once
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
                logger.EmailNackFailed(nackEx);
            }
        }
    }

    private async Task NackWithoutRequeueAsync(IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        try
        {
            // requeue:false is itself an immediate dead-letter reason, distinct from delivery-limit
            // exhaustion - a malformed message goes straight to email-delivery.failed, no wasted
            // retries on a payload that will never deserialize.
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailNackFailed(ex);
        }
    }
}
