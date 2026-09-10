namespace PatientBooking.Api.BackgroundServices;

internal static partial class RegistrationOutboxDispatcherLoggerExtensions
{
    [LoggerMessage(EventId = 3223, Level = LogLevel.Warning, Message = "Registration outbox dispatch loop failed - retrying shortly.")]
    public static partial void RegistrationOutboxDispatchLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3224, Level = LogLevel.Warning, Message = "Registration outbox message {NotificationId} could not be deserialized - leaving it for manual inspection.")]
    public static partial void RegistrationOutboxMessageMalformed(
        this ILogger logger,
        Exception exception,
        Guid notificationId
    );
}
