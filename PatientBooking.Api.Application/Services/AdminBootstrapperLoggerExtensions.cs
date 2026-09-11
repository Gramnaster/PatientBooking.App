using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Services;

internal static partial class AdminBootstrapperLoggerExtensions
{
    [LoggerMessage(EventId = 3500, Level = LogLevel.Error, Message = "Admin bootstrap skipped: the configured email already belongs to a non-admin account. " +
        "Use an unused ADMIN_SEED_EMAIL and redeploy. The API will start without creating an admin.")]
    public static partial void AdminBootstrapSkippedEmailInUse(this ILogger logger);
}
