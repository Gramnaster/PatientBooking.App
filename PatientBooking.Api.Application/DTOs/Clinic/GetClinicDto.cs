using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Clinic;

public sealed record GetClinicDto(int Id, string Name, string Address);
