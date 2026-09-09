namespace PatientBooking.Api.BackgroundServices;

internal static partial class BookingConfirmationConsumerLoggerExtensions
{
    [LoggerMessage(EventId = 3204, Level = LogLevel.Warning, Message = "Booking-confirmation consumer loop failed - retrying shortly.")]
    public static partial void BookingConfirmationConsumerLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3205, Level = LogLevel.Warning, Message = "Failed to handle a booking-confirmed message.")]
    public static partial void BookingConfirmationMessageHandlingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3206, Level = LogLevel.Error, Message = "Failed to Nack a booking-confirmed message after handling it failed.")]
    public static partial void BookingConfirmationNackFailed(this ILogger logger, Exception exception);
}
