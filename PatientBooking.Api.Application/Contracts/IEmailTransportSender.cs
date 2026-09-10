namespace PatientBooking.Api.Application.Contracts;

// The actual SMTP/SendGrid transport - distinct from IEmailEventPublisher, which only queues a
// message onto the broker. The consumer must call this, never the publisher, to perform a send.
public interface IEmailTransportSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, string? plainTextBody, CancellationToken ct);
}
