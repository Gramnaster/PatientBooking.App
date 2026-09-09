using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PatientBooking.Api.BackgroundServices;

public sealed class BookingConfirmationConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<BookingConfirmationConsumer> logger
) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

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
            await channel.QueueDeclareAsync(
                RabbitMqBookingEventPublisher.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: RabbitMqBookingEventPublisher.QueueArguments,
                cancellationToken: ct
            );

            // Some in-flight at once than unbounded.
            // RabbitMQ pushes every waiting message to consumer with no prefetch limit set.
            await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 5,
                    global: false,
                    cancellationToken: ct
            );

            AsyncEventingBasicConsumer consumer = new(channel);
            consumer.ReceivedAsync += async (_, ea) => await HandleDeliveryAsync(channel, ea, ct);

            await channel.BasicConsumeAsync(
                RabbitMqBookingEventPublisher.QueueName,
                autoAck: false,
                consumer,
                cancellationToken: ct
            );

            // BasicConsumeAsync returns once consumer is registered
            // Stay here until shutdown or channel drops out from under us
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
                // idleSignal fired because channel shut down, not ct
                // fall through so outer loop retries with a fresh channel after RetryDelay
            }
        }
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            BookingConfirmedEvent? evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(ea.Body.Span);

            if (evt is not null)
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<IBookingNotificationSender>();

                await sender.SendBookingConfirmationAsync(evt, ct);
            }

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.BookingConfirmationMessageHandlingFailed(ex);

            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
            }
            catch (Exception nackEx) when (nackEx is not OperationCanceledException)
            {
                logger.BookingConfirmationNackFailed(nackEx);
            }
        }
    }
}
