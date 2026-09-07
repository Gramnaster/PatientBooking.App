using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Clinic;

public record CreateClinicDto
{
    [Required]
    [MaxLength(255)]
    public required string Name { get; set; }
    [Required]
    [MaxLength(255)]
    public required string Address { get; set; }
}
