namespace PatientBooking.Api.Application.Messaging;

public sealed record BookingConfirmedEvent(
    Guid NotificationId,
    int BookingId,
    string BookingNumber,
    string PatientEmail,
    string PatientFullName,
    string ClinicName,
    DateTimeOffset AppointmentStartUtc,
    decimal TotalPrice
);
