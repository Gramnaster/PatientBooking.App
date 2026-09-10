namespace PatientBooking.Api.Application.Messaging;

// Wire contract for every background-delivered email. A feature composes Subject/HtmlBody itself
// (see BookingServices/UsersService) - this envelope only carries the fully-rendered result, so the
// consumer never needs to know what kind of email it's sending.
// Kind is optional and diagnostic-only (structured log property) - the consumer must never switch
// on it to rebuild per-feature business logic.
public sealed record EmailEnvelope(
    Guid NotificationId,
    string Recipient,
    string Subject,
    string HtmlBody,
    string? PlainTextBody,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? Kind
);
