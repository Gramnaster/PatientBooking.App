using PatientBooking.Api.Application.Messaging;

namespace PatientBooking.Api.Application.Contracts;

public interface IBookingNotificationSender
{
    Task SendBookingConfirmationAsync(BookingConfirmedEvent evt, CancellationToken ct);
}
