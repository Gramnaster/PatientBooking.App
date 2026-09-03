namespace PatientBooking.Api.Application.DTOs.Auth;

public sealed record RefreshTokenSessionDto
{
    public int Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
