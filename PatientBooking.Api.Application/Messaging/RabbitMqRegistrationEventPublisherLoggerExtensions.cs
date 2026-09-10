using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Messaging;

internal static partial class RabbitMqRegistrationEventPublisherLoggerExtensions
{
    [LoggerMessage(EventId = 3220, Level = LogLevel.Warning, Message = "Failed to publish registration-confirmation event for user {UserId} - no channel available.")]
    public static partial void RegistrationConfirmationPublishSkipped(this ILogger logger, string userId);

    [LoggerMessage(EventId = 3221, Level = LogLevel.Warning, Message = "Failed to publish registration-confirmation event for user {UserId}.")]
    public static partial void RegistrationConfirmationPublishFailed(
        this ILogger logger,
        Exception exception,
        string userId
    );

    [LoggerMessage(EventId = 3222, Level = LogLevel.Warning, Message = "Registration-confirmation event for user {UserId} was returned as unroutable: {ReplyText}")]
    public static partial void RegistrationConfirmationPublishReturned(
        this ILogger logger,
        string userId,
        string replyText
    );
}
