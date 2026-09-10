namespace PatientBooking.Api.Domain;

public class RegistrationOutboxMessage
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public int DispatchAttempts { get; set; }
    public string? LastError { get; set; }
}
