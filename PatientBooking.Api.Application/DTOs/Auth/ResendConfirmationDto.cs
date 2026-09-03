using System.ComponentModel.DataAnnotations;

namespace PatientBooking.Api.Application.DTOs.Auth;

public class ResendConfirmationDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
