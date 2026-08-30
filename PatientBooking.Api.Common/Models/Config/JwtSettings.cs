namespace PatientBooking.Api.Common.Models.Config;

public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int DurationInMinutes { get; set; }
    public int RefreshTokenDurationInDays { get; set; }
}
