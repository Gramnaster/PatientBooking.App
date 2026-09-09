using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PatientBooking.Api.Application.Contracts;
using RabbitMQ.Client;

namespace PatientBooking.Api.Application.Messaging;

public sealed class RabbitMqBookingEventPublisher(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqBookingEventPublisher> logger
) : IBookingEventPublisher
{
    public const string QueueName = "booking-confirmed";

    public static readonly ReadOnlyDictionary<string, object?> QueueArguments = new(
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-queue-type"] = "quorum" }
    );
    public async Task PublishBookingConfirmedAsync(BookingConfirmedEvent evt, CancellationToken ct)
    {
        IChannel? channel = await connectionProvider.TryOpenChannelAsync(ct);
        if (channel is null)
        {
            logger.BookingConfirmedPublishSkipped(evt.BookingId);
            return;
        }

        await using (channel.ConfigureAwait(false))
        {
            try
            {
                await channel.QueueDeclareAsync(
                    QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: QueueArguments,
                    cancellationToken: ct
                );

                byte[] body = JsonSerializer.SerializeToUtf8Bytes(evt);
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: QueueName,
                    body: body,
                    cancellationToken: ct
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.BookingConfirmedPublishFailed(ex, evt.BookingId);
            }
        }
    }
}
