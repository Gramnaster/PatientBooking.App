namespace PatientBooking.Api.BackgroundServices;

internal static partial class BookingConfirmationConsumerLoggerExtensions
{
    [LoggerMessage(EventId = 3204, Level = LogLevel.Warning, Message = "Booking-confirmation consumer loop failed - retrying shortly.")]
    public static partial void BookingConfirmationConsumerLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3205, Level = LogLevel.Warning, Message = "Failed to handle a booking-confirmed message.")]
    public static partial void BookingConfirmationMessageHandlingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3206, Level = LogLevel.Error, Message = "Failed to Nack a booking-confirmed message after handling it failed.")]
    public static partial void BookingConfirmationNackFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3208, Level = LogLevel.Information, Message = "Notification {NotificationId} already sent - skipping duplicate delivery.")]
    public static partial void BookingConfirmationDuplicateSkipped(this ILogger logger, Guid notificationId);

    [LoggerMessage(EventId = 3209, Level = LogLevel.Error, Message = "Booking-confirmed message could not be deserialized - routing to the failed queue.")]
    public static partial void BookingConfirmationMessageMalformed(this ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 3210, Level = LogLevel.Warning, Message = "Consumer {ConsumerTag} was cancelled by the broker - redeclaring and resuming.")]
    public static partial void BookingConfirmationConsumerCancelled(this ILogger logger, string consumerTag);
}
