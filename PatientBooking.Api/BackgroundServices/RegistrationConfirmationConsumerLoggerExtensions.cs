namespace PatientBooking.Api.BackgroundServices;

internal static partial class RegistrationConfirmationConsumerLoggerExtensions
{
    [LoggerMessage(EventId = 3225, Level = LogLevel.Warning, Message = "Registration-confirmation consumer loop failed - retrying shortly.")]
    public static partial void RegistrationConfirmationConsumerLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3226, Level = LogLevel.Warning, Message = "Failed to handle a registration-confirmation message.")]
    public static partial void RegistrationConfirmationMessageHandlingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3227, Level = LogLevel.Error, Message = "Failed to Nack a registration-confirmation message after handling it failed.")]
    public static partial void RegistrationConfirmationNackFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3228, Level = LogLevel.Information, Message = "Notification {NotificationId} already sent - skipping duplicate delivery.")]
    public static partial void RegistrationConfirmationDuplicateSkipped(this ILogger logger, Guid notificationId);

    [LoggerMessage(EventId = 3229, Level = LogLevel.Error, Message = "Registration-confirmation message could not be deserialized - routing to the failed queue.")]
    public static partial void RegistrationConfirmationMessageMalformed(this ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 3230, Level = LogLevel.Warning, Message = "Consumer {ConsumerTag} was cancelled by the broker - redeclaring and resuming.")]
    public static partial void RegistrationConfirmationConsumerCancelled(this ILogger logger, string consumerTag);
}
