using System.Globalization;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public sealed class SmtpIdentityEmailSender(
    IOptions<EmailSettings> emailOptions,
    ILogger<SmtpIdentityEmailSender> logger
) : IEmailSender<ApplicationUser>, ILoginNotificationSender, IBookingNotificationSender
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(
            email,
            "Confirm your email",
            $"""Please confirm your Patient Booking account by <a href="{confirmationLink}">clicking here</a>"""
        );

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendEmailAsync(
            email,
            "Reset your password",
            $"""Please reset your password by <a href="{resetCode}">clicking here</a>"""
        );

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(
            email,
            "Reset your password",
            $"""Please reset your password by <a href="{resetLink}">clicking here</a>"""
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
            await SendEmailAsync(
                user.Email!,
                "New sign-in to your account",
                $"Your Patient Booking account was just signed into from IP {ipAddress} at {occuredAtUtc:u} UTC." +
                    "If this wasn't you, reset your password immediately and review your active sessions.",
                ct
            );
        }
        catch (Exception ex) when (ex is SocketException or SmtpCommandException or SmtpProtocolException or IOException)
        {
            // Runs after login already committed the refresh token - a failed notification must not fail the login.
            logger.LoginNotificationSendFailed(ex, user.Email!);
        }
    }

    public Task SendBookingConfirmationAsync(BookingConfirmedEvent evt, CancellationToken ct)
    {
        return SendEmailAsync(
            evt.PatientEmail,
            $"Booking confirmed - {evt.BookingNumber}",
            $"Hi {evt.PatientFullName}, your booking {evt.BookingNumber} at {evt.ClinicName} is confirmed for" + string.Create(
                CultureInfo.InvariantCulture,
                $"{evt.AppointmentStartUtc:u} UTC. Total: {evt.TotalPrice:C}."
            ),
            ct
        );
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        EmailSettings settings = emailOptions.Value;

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

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
