namespace PatientBooking.Api.Domain;

// Dedup record for every background-delivered email. Id is the notification's stable id
// (EmailOutboxMessage.Id / EmailEnvelope.NotificationId) - its existence means the email already
// went out, so a redelivered message can be safely ack'd without resending.
public class SentEmailNotification
{
    public Guid Id { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
