namespace PatientBooking.Api.Domain;

public class Admin
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string? AdminNumber { get; set; };
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? DateTimeOffset { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
