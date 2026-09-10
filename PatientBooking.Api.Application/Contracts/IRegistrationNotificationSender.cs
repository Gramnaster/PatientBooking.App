using PatientBooking.Api.Application.Messaging;

namespace PatientBooking.Api.Application.Contracts;

public interface IRegistrationNotificationSender
{
    Task SendRegistrationConfirmationAsync(RegistrationConfirmationEvent evt, CancellationToken ct);
}
