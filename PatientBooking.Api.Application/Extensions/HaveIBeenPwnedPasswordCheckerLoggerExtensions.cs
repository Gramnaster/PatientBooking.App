using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Services;

internal static partial class HaveIBeenPwnedPasswordCheckerLoggerExtensions
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "Breached-password check failed; allowing the password through.")]
    public static partial void BreachCheckFailed(this ILogger logger, Exception exception);
}
