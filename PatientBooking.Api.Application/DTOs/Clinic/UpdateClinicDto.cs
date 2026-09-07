using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Clinic;

public sealed record UpdateClinicDto : CreateClinicDto
{
    [Required]
    public required int Id { get; set; }
}
