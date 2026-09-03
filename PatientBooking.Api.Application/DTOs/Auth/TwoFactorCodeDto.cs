using System.ComponentModel.DataAnnotations;

namespace PatientBooking.Api.Application.DTOs.Auth;

/// <summary>
/// Inbound shape shared by POST /api/auth/2fa/enable and POST /api/auth/2fa/login.
/// Code accepts either 6-digit TOTP code or recovery code, since the two formats
/// never collide and the caller shouldn't need to say which kind it's sending.
/// </summary>
public sealed record TwoFactorCodeDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
}
