using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Handlers;

internal static partial class GlobalExceptionHandlerLoggerExtensions
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Error, Message = "Unhandled exception on {RequestPath}.")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string requestPath);
}
