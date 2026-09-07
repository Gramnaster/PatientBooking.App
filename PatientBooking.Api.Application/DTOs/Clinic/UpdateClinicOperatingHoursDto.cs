using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Clinic;

public sealed record UpdateClinicOperatingHoursDto
{
    [Required]
    public required DayOfWeek DayOfWeek { get; set; }

    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}
