using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Messaging;

internal static partial class RabbitMqConnectionProviderLoggerExtensions
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Warning, Message = "RabbitMQ broker unreacehable. Continuing without it.")]
    public static partial void RabbitMqConnectFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Warning, Message = "Failed to open a RabbitMQ channel.")]
    public static partial void RabbitMqChannelFailed(this ILogger logger, Exception exception);
}
