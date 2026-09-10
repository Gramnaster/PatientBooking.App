namespace PatientBooking.Api.BackgroundServices;

internal static partial class EmailOutboxDispatcherLoggerExtensions
{
    [LoggerMessage(EventId = 3303, Level = LogLevel.Warning, Message = "Email outbox dispatch loop failed - retrying shortly.")]
    public static partial void EmailOutboxDispatchLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3304, Level = LogLevel.Warning, Message = "Email outbox message {NotificationId} could not be deserialized - leaving it for manual inspection.")]
    public static partial void EmailOutboxMessageMalformed(
        this ILogger logger,
        Exception exception,
        Guid notificationId
    );

    [LoggerMessage(EventId = 3305, Level = LogLevel.Warning, Message = "Email outbox message {NotificationId} expired before it could be dispatched - marking abandoned without sending.")]
    public static partial void EmailOutboxMessageExpired(this ILogger logger, Guid notificationId);
}
