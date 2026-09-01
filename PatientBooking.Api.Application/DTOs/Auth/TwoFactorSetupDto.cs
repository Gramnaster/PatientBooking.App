using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Auth;

/// <summary>
/// Outbound shape returned by GET /api/auth/2fa/setup
/// </summary>
public sealed record TwoFactorSetupDto
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
}
