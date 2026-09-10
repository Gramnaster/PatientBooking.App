namespace PatientBooking.Api.BackgroundServices;

internal static partial class EmailDeliveryConsumerLoggerExtensions
{
    [LoggerMessage(EventId = 3306, Level = LogLevel.Warning, Message = "Email consumer loop failed - retrying shortly.")]
    public static partial void EmailConsumerLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3307, Level = LogLevel.Warning, Message = "Failed to handle an email-delivery message.")]
    public static partial void EmailMessageHandlingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3308, Level = LogLevel.Error, Message = "Failed to Nack an email-delivery message after handling it failed.")]
    public static partial void EmailNackFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3309, Level = LogLevel.Information, Message = "Notification {NotificationId} already sent - skipping duplicate delivery.")]
    public static partial void EmailDuplicateSkipped(this ILogger logger, Guid notificationId);

    [LoggerMessage(EventId = 3310, Level = LogLevel.Error, Message = "Email-delivery message could not be deserialized - routing to the failed queue.")]
    public static partial void EmailMessageMalformed(this ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 3311, Level = LogLevel.Warning, Message = "Consumer {ConsumerTag} was cancelled by the broker - redeclaring and resuming.")]
    public static partial void EmailConsumerCancelled(this ILogger logger, string consumerTag);

    [LoggerMessage(EventId = 3312, Level = LogLevel.Warning, Message = "Email {NotificationId} expired before it could be sent - discarding without recording delivery.")]
    public static partial void EmailMessageExpired(this ILogger logger, Guid notificationId);
}
