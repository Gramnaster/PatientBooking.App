using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Auth;

public sealed record ExternalLoginDto
{
    [Required]
    public string Provider { get; set; } = string.Empty;

    [Required]
    public string IdToken { get; set; } = string.Empty;
}
