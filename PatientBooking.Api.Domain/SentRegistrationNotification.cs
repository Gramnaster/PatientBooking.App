namespace PatientBooking.Api.Domain;

// Dedup record for registration-confirmation emails. Id is the notification's stable id
// (RegistrationOutboxMessage.Id / RegistrationConfirmationEvent.NotificationId) - its existence
// means the email already went out, so a redelivered message can be safely ack'd without resending.
public class SentRegistrationNotification
{
    public Guid Id { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
