namespace PatientBooking.Api.Application.Messaging;

public sealed record BookingConfirmedEvent
(
    int BookingId,
    string BookingNumber,
    string PatientEmail,
    string PatientFullName,
    string ClinicName,
    DateTimeOffset AppointmentStartUtc,
    decimal TotalPrice
);
