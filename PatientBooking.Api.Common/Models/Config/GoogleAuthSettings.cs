using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Common.Models.Config;

// Bound from "Authentication:Google", set via dotnet user-secrets in dev,
// but never in appsettings.json. Only ClientId - ExternalLoginAsync verifies
// a Google-issued id_token by signature (GoogleJsonWebSignature.ValidateAsync),
// which only needs the ClientId to check the token's audience. No client secret
// exchange happens server-side.
public class GoogleAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
}
