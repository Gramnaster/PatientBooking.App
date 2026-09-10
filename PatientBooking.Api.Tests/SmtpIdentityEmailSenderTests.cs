using System.Net;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Tests.Infrastructure;
using Xunit;

namespace PatientBooking.Api.Tests;

// Regression tests for the 2026-09-10 email-flow review: a password-reset code must never render
// as a hyperlink, confirmation/reset links must be HTML-encoded without corrupting the token, and
// login notifications must not glue two sentences together or double up the UTC timezone label.
// Real SMTP via smtp4dev (same image as MessagingTestFixture/local dev) - no mock IEmailSender, so
// a bug in the actual rendered body fails these tests the same way it would fail in production.
[Collection(EmailCollection.Name)]
public sealed class SmtpIdentityEmailSenderTests(EmailTestFixture fixture)
{
    private static ApplicationUser TestUser(string email) => new() { Email = email, UserName = email };

    [Fact]
    public async Task SendPasswordResetCodeAsync_Always_RendersCodeAsPlainTextNotHyperlink()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        const string email = "reset-code@patientbooking.test";
        const string resetCode = "ABC123XYZ";

        await fixture.Sender.SendPasswordResetCodeAsync(TestUser(email), email, resetCode);
        string body = await fixture.GetLatestHtmlBodyAsync(email, ct);

        Assert.Contains(resetCode, body, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_LinkHasQueryString_EncodesHrefAndPreservesLink()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        const string email = "reset-link@patientbooking.test";
        const string resetLink = "https://patientbooking.test/api/auth/reset-password?userId=abc&token=xyz";

        await fixture.Sender.SendPasswordResetLinkAsync(TestUser(email), email, resetLink);
        string body = await fixture.GetLatestHtmlBodyAsync(email, ct);

        // Raw, un-encoded "&" between query params would mean the href attribute is broken HTML -
        // it must never survive into the rendered body as-is.
        Assert.DoesNotContain("userId=abc&token", body, StringComparison.Ordinal);
        Assert.Equal(resetLink, WebUtility.HtmlDecode(ExtractHref(body)));
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_LinkHasQueryString_EncodesHrefAndPreservesLink()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        const string email = "confirm-link@patientbooking.test";
        const string confirmationLink = "https://patientbooking.test/api/auth/confirm-email?userId=abc&token=xyz";

        await fixture.Sender.SendConfirmationLinkAsync(TestUser(email), email, confirmationLink);
        string body = await fixture.GetLatestHtmlBodyAsync(email, ct);

        Assert.DoesNotContain("userId=abc&token", body, StringComparison.Ordinal);
        Assert.Equal(confirmationLink, WebUtility.HtmlDecode(ExtractHref(body)));
    }

    [Fact]
    public async Task SendLoginNotificationAsync_Always_HasSpaceBeforeNextSentenceAndSingleTimezoneLabel()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        const string email = "login-notify@patientbooking.test";

        await fixture.Sender.SendLoginNotificationAsync(TestUser(email), "203.0.113.5", DateTimeOffset.UtcNow, ct);
        string body = await fixture.GetLatestHtmlBodyAsync(email, ct);

        // ":u" renders "...ssZ" - a trailing literal " UTC" on top of that is a duplicate label, and
        // the original bug also glued the next sentence directly onto the "." with no space.
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}Z\. If this wasn't you", body);
        Assert.DoesNotContain("UTC", body, StringComparison.Ordinal);
    }

    private static string ExtractHref(string html)
    {
        const string marker = "href=\"";
        int start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = html.IndexOf('"', start);
        return html[start..end];
    }
}
