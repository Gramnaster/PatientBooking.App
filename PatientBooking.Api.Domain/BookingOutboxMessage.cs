namespace PatientBooking.Api.Domain;

public class BookingOutboxMessage
{
    public Guid Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public int DispatchAttempts { get; set; }
    public string? LastError { get; set; }
}
