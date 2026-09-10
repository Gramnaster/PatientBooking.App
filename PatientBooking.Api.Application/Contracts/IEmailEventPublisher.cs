using PatientBooking.Api.Application.Messaging;

namespace PatientBooking.Api.Application.Contracts;

public interface IEmailEventPublisher
{
    // Returns true only once the broker has confirmed the publish (and it routed) - false on any
    // failure (broker unreachable, nack, or an unroutable/returned message) so the outbox dispatcher
    // knows the row is still undelivered and should be retried.
    Task<bool> PublishEmailAsync(EmailEnvelope evt, CancellationToken ct);
}
