using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PatientBooking.Api.BackgroundServices;

// Delivers registration-confirmation emails, mirroring BookingConfirmationConsumer's confirmed
// publish / manual-ack / retry / dead-letter / dedup design. No legacy-message compatibility code
// is needed here - registration-confirmation is a brand new queue with no messages predating
// RegistrationConfirmationEvent's NotificationId field.
public sealed class RegistrationConfirmationConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<RegistrationConfirmationConsumer> logger
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
                logger.RegistrationConfirmationConsumerLoopFailed(ex);
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
            await RabbitMqRegistrationEventPublisher.DeclareTopologyAsync(channel, ct);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5, global: false, cancellationToken: ct);

            AsyncEventingBasicConsumer consumer = new(channel);
            consumer.ReceivedAsync += async (_, ea) => await HandleDeliveryAsync(channel, ea, ct);

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

            consumer.UnregisteredAsync += (_, ea) =>
            {
                logger.RegistrationConfirmationConsumerCancelled(string.Join(',', ea.ConsumerTags));

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
                RabbitMqRegistrationEventPublisher.QueueName,
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
        RegistrationConfirmationEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<RegistrationConfirmationEvent>(ea.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.RegistrationConfirmationMessageMalformed(ex);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
            return;
        }

        if (
            evt is null || evt.NotificationId == Guid.Empty || string.IsNullOrWhiteSpace(
                evt.UserId
            ) || string.IsNullOrWhiteSpace(evt.Email) || string.IsNullOrWhiteSpace(evt.ConfirmationLink)
        )
        {
            logger.RegistrationConfirmationMessageMalformed(null);
            await NackWithoutRequeueAsync(channel, ea.DeliveryTag, ct);
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

            bool alreadySent = await db.SentRegistrationNotifications.AnyAsync(n => n.Id == evt.NotificationId, ct);
            if (alreadySent)
            {
                logger.RegistrationConfirmationDuplicateSkipped(evt.NotificationId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            var sender = scope.ServiceProvider.GetRequiredService<IRegistrationNotificationSender>();
            await sender.SendRegistrationConfirmationAsync(evt, ct);

            try
            {
                db.Add(new SentRegistrationNotification { Id = evt.NotificationId, SentAtUtc = clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                // Genuine duplicate-key race: a concurrent redelivery beat us to recording this
                // dedup row. The email already went out (by us or by it) either way, so ack rather
                // than retry a send that already happened.
                logger.RegistrationConfirmationDuplicateSkipped(evt.NotificationId);
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
            logger.RegistrationConfirmationMessageHandlingFailed(ex);

            // Short delay before the broker-counted redelivery, not a hand-rolled attempt counter -
            // the registration-confirmation-retry-limit policy dead-letters to
            // registration-confirmation.failed once RabbitMQ's own delivery-limit is exceeded. This
            // must be basic.reject, not basic.nack - see BookingConfirmationConsumer for the same
            // reasoning against the same RabbitMQ 4.3 quorum-queue delivery-count semantics.
            await Task.Delay(SendFailureDelay, ct);

            try
            {
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: true, ct);
            }
            catch (Exception nackEx) when (nackEx is not OperationCanceledException)
            {
                logger.RegistrationConfirmationNackFailed(nackEx);
            }
        }
    }

    private async Task NackWithoutRequeueAsync(IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RegistrationConfirmationNackFailed(ex);
        }
    }
}
