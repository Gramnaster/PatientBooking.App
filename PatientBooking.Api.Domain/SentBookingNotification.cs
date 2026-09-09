namespace PatientBooking.Api.Domain;

// Dedup record for booking-confirmation emails. Id is the notification's stable id
// (BookingOutboxMessage.Id / BookingConfirmedEvent.NotificationId) - its existence means
// the email already went out, so a redelivered message can be safely ack'd without resending.
public class SentBookingNotification
{
    public Guid Id { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
