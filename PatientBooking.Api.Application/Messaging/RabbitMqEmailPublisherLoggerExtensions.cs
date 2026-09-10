using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Messaging;

internal static partial class RabbitMqEmailPublisherLoggerExtensions
{
    [LoggerMessage(EventId = 3300, Level = LogLevel.Warning, Message = "Failed to publish email {NotificationId} - no channel available.")]
    public static partial void EmailPublishSkipped(this ILogger logger, Guid notificationId);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Warning, Message = "Failed to publish email {NotificationId}.")]
    public static partial void EmailPublishFailed(this ILogger logger, Exception exception, Guid notificationId);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Warning, Message = "Email {NotificationId} was returned as unroutable: {ReplyText}")]
    public static partial void EmailPublishReturned(this ILogger logger, Guid notificationId, string replyText);
}
