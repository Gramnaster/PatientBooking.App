namespace PatientBooking.Api.Domain;

// Shared outbox for every background-delivered email (booking confirmation, registration
// confirmation, and any future kind) - deliberately carries no domain-specific foreign key. A
// feature composes its own Subject/HtmlBody before staging one of these in its own transaction.
public class EmailOutboxMessage
{
    public Guid Id { get; set; }

    // Denormalized from the envelope inside Payload - lets an operator find "was this email queued"
    // by recipient without parsing JSON. Not a foreign key: every email kind populates it the same
    // way, so it stays a shared, not domain-specific, field.
    public string Recipient { get; set; } = string.Empty;

    // Diagnostic only - never switched on by the dispatcher or consumer. Lets an operator (or a
    // test) tell which of several rows for the same recipient is which, without parsing Payload.
    public string? Kind { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }

    // Set only when the envelope's ExpiresAtUtc passed before the dispatcher could publish it -
    // distinct from DispatchedAtUtc so an expired row stops being polled without ever being
    // recorded as sent. Mutually exclusive with DispatchedAtUtc in practice.
    public DateTimeOffset? ExpiredAtUtc { get; set; }

    public int DispatchAttempts { get; set; }
    public string? LastError { get; set; }
}
