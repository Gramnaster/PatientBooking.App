namespace PatientBooking.Api.Application.DTOs.Booking;

// PatientId is not present here. Caller's own Patient profile is resolved server-side
// so patient can never book on another patient's behalf by giving a different id
public sealed record CreateBookingDto
{
    public required int ClinicId { get; set; }
    public required DateTimeOffset AppointmentStartUtc { get; set; }
}
