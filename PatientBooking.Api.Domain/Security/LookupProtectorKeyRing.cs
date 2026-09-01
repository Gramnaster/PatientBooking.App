using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PatientBooking.Api.Common.Models.Config;

namespace PatientBooking.Api.Domain.Security;

public sealed class LookupProtectorKeyRing : ILookupProtectorKeyRing
{
    public const string DefaultKeyId = "default";
    private readonly string key;

    public LookupProtectorKeyRing(IOptions<LookupProtectionSettings> options)
    {
        key = options.Value.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("LookupProtection:Key is not configured.");
        }
    }
    public string this[string keyId] => key;

    public string CurrentKeyId => DefaultKeyId;

    public IEnumerable<string> GetAllKeyIds()
    {
        return [DefaultKeyId];
    }
}
