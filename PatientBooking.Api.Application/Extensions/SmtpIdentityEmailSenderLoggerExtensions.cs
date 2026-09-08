using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Services;

internal static partial class SmtpIdentityEmailSenderLoggerExtensions
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning, Message = "Failed to send login notification email to {Recipient}.")]
    public static partial void LoginNotificationSendFailed(this ILogger logger, Exception exception, string recipient);
}
