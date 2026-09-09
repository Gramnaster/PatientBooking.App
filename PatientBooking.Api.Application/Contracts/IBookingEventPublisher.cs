using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using PatientBooking.Api.Application.Messaging;

namespace PatientBooking.Api.Application.Contracts;

public interface IBookingEventPublisher
{
    Task PublishBookingConfirmedAsync(BookingConfirmedEvent evt, CancellationToken ct);
}
