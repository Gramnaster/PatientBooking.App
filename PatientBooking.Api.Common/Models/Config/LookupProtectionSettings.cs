namespace PatientBooking.Api.Common.Models.Config;

// Backs LookupProtectorKeyRing - Key is absent from appsettings.json, same handling as
// JwtSettings:Key. Must be a Base64-encoded value at least 32 bytes (256 bits) once decoded,
// since fed straight into HMACSHA256 as the key material
public class LookupProtectionSettings
{
    public string Key { get; set; } = string.Empty;
}
