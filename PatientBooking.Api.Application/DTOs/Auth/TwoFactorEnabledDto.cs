namespace PatientBooking.Api.Application.DTOs.Auth;

/// <summary>
/// Outbound shape returned by POST /api/auth/2fa/enable.
/// Identity never stores or returns the plaintext codes again after this.
/// </summary>
public sealed record TwoFactorEnabledDto
{
    public IEnumerable<string> RecoveryCodes { get; set; } = [];
}
