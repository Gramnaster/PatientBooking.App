using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Booking;

// ClinicId comes from staff within their own clinic, not the body
// IdempotencyKey travels as the Idempotency-Key request header
public sealed record CreateBookingForPatientDto
{
    public required int PatientId { get; set; }
    public required DateTimeOffset AppointmentStartUtc { get; set; }
}
