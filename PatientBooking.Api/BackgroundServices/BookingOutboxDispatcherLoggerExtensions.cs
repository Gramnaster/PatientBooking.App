namespace PatientBooking.Api.BackgroundServices;

internal static partial class BookingOutboxDispatcherLoggerExtensions
{
    [LoggerMessage(EventId = 3211, Level = LogLevel.Warning, Message = "Booking outbox dispatch loop failed - retrying shortly.")]
    public static partial void BookingOutboxDispatchLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3212, Level = LogLevel.Warning, Message = "Booking outbox message {NotificationId} could not be deserialized - leaving it for manual inspection.")]
    public static partial void BookingOutboxMessageMalformed(
        this ILogger logger,
        Exception exception,
        Guid notificationId
    );
}
