using System.Net;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public sealed class SmtpIdentityEmailSender(
    IOptions<EmailSettings> emailOptions,
    ILogger<SmtpIdentityEmailSender> logger
) : IEmailSender<ApplicationUser>, ILoginNotificationSender, IEmailTransportSender
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(
            email,
            "Confirm your email",
            $"""Please confirm your Patient Booking account by <a href="{WebUtility.HtmlEncode(confirmationLink)}">clicking here</a>""",
            plainTextBody: null,
            CancellationToken.None
        );

    // A reset CODE is meant to be typed into the app, not clicked - it must never render as a link.
    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(
            email,
            "Reset your password",
            $"""Your Patient Booking password reset code is: <strong>{WebUtility.HtmlEncode(resetCode)}</strong>. Enter it in the app to reset your password.""",
            plainTextBody: null,
            CancellationToken.None
        );

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(
            email,
            "Reset your password",
            $"""Please reset your password by <a href="{WebUtility.HtmlEncode(resetLink)}">clicking here</a>""",
            plainTextBody: null,
            CancellationToken.None
        );

    public async Task SendLoginNotificationAsync(
        ApplicationUser user,
        string ipAddress,
        DateTimeOffset occuredAtUtc,
        CancellationToken ct
    )
    {
        try
        {
            await SendAsync(
                user.Email!,
                "New sign-in to your account",
                // ":u" already appends a trailing "Z" (UniversalSortableDateTimePattern) - do not also append "UTC".
                $"Your Patient Booking account was just signed into from IP {ipAddress} at {occuredAtUtc:u}. " +
                    "If this wasn't you, reset your password immediately and review your active sessions.",
                plainTextBody: null,
                ct
            );
        }
        catch (Exception ex) when (ex is SocketException or SmtpCommandException or SmtpProtocolException or IOException)
        {
            // Runs after login already committed the refresh token - a failed notification must not fail the login.
            logger.LoginNotificationSendFailed(ex, user.Email!);
        }
    }

    // The actual transport - staged emails (booking/registration confirmation, and any future kind)
    // reach this only through EmailDeliveryConsumer, never directly from a request. Composition
    // (Subject/HtmlBody) is the caller's responsibility; this method only knows how to send.
    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody,
        CancellationToken ct
    )
    {
        EmailSettings settings = emailOptions.Value;

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = string.IsNullOrEmpty(plainTextBody)
            ? new TextPart(TextFormat.Html) { Text = htmlBody }
            : new MultipartAlternative
            {
                new TextPart(TextFormat.Plain) { Text = plainTextBody },
                new TextPart(TextFormat.Html) { Text = htmlBody },
            };

        // SmtpClient here uses MimeKit
        using var client = new SmtpClient();

        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, ct);

        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
