namespace PatientBooking.Api.Application.Messaging;

public sealed record RegistrationConfirmationEvent(
    Guid NotificationId,
    string UserId,
    string Email,
    string ConfirmationLink
);
