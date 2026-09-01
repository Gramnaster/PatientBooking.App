using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Services;

internal static partial class UsersServiceLoggerExtensions
{
    [LoggerMessage(EventId = 3101, Level = LogLevel.Warning, Message = "Invalid two-factor code for user {UserId}")]
    public static partial void InvalidTwoFactorCode(this ILogger logger, string userId);
    [LoggerMessage(EventId = 3107, Level = LogLevel.Debug, Message = "Incorrect password usage for email: {Email} from {IpAddress}")]
    public static partial void LoginFailedWrongPassword(this ILogger logger, string email, string ipAddress);

    [LoggerMessage(EventId = 3108, Level = LogLevel.Debug, Message = "Login blocked - account locked for email: {Email} from {IpAddress}")]
    public static partial void LoginBlockedLockedOut(this ILogger logger, string email, string ipAddress);

    [LoggerMessage(EventId = 3019, Level = LogLevel.Debug, Message = "Login blocked for unconfirmed email: {Email} from {IpAddress}")]
    public static partial void LoginBlockedEmailNotConfirmed(this ILogger logger, string email, string ipAddress);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId} - revoking all active tokens.")]
    public static partial void RefreshTokenReuseDetected(this ILogger logger, string userId);
}