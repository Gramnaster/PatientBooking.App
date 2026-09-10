using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PatientBooking.Api.Application.Contracts;
using RabbitMQ.Client;

namespace PatientBooking.Api.Application.Messaging;

public sealed class RabbitMqRegistrationEventPublisher(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqRegistrationEventPublisher> logger
) : IRegistrationEventPublisher
{
    public const string QueueName = "registration-confirmation";
    public const string FailedQueueName = "registration-confirmation.failed";
    public const string DeadLetterExchangeName = "registration-confirmation.dlx";

    public static readonly ReadOnlyDictionary<string, object?> QueueArguments = new(new Dictionary<string, object?>(
        StringComparer.Ordinal
    )
    { ["x-queue-type"] = "quorum" });

    public async Task<bool> PublishRegistrationConfirmationAsync(
        RegistrationConfirmationEvent evt,
        CancellationToken ct
    )
    {
        IChannel? channel = await connectionProvider.TryOpenChannelAsync(ct, publisherConfirms: true);
        if (channel is null)
        {
            logger.RegistrationConfirmationPublishSkipped(evt.UserId);
            return false;
        }

        await using (channel.ConfigureAwait(false))
        {
            // mandatory:true asks the broker to basic.return the message instead of silently
            // dropping it if it can't be routed. The confirm still arrives either way - a returned
            // message is still ack'd - so this flag is the only signal an unroutable publish gets.
            bool returned = false;
            channel.BasicReturnAsync += (_, ea) =>
            {
                returned = true;
                logger.RegistrationConfirmationPublishReturned(evt.UserId, ea.ReplyText);
                return Task.CompletedTask;
            };

            try
            {
                await DeclareTopologyAsync(channel, ct);

                byte[] body = JsonSerializer.SerializeToUtf8Bytes(evt);
                BasicProperties properties = new() { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: QueueName,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: ct
                );

                return !returned;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.RegistrationConfirmationPublishFailed(ex, evt.UserId);
                return false;
            }
        }
    }

    // Declared defensively on every publish, same as booking-confirmed - cheap and idempotent. The
    // failed queue only needs to exist; its retry/dead-letter behavior comes from a broker policy
    // applied once via rabbitmqctl set_policy (see docs/deployment.md's RabbitMQ section), not from
    // arguments on registration-confirmation itself.
    public static async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: QueueArguments,
            cancellationToken: ct
        );

        await channel.ExchangeDeclareAsync(
            DeadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: ct
        );

        await channel.QueueDeclareAsync(
            FailedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: QueueArguments,
            cancellationToken: ct
        );

        await channel.QueueBindAsync(FailedQueueName, DeadLetterExchangeName, FailedQueueName, cancellationToken: ct);
    }
}
