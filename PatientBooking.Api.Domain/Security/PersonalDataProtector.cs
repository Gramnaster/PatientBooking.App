using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace PatientBooking.Api.Domain.Security;

// Non-deterministic, wraps IDataProtector. Same plaintext encrypts to different
// ciphertext every time (random IV). Safe for Email/UserName/PhoneNumber/FirstName/LastName
// because none of those columns are ever compared with == in a query.
public sealed class PersonalDataProtector : IPersonalDataProtector
{
    private readonly IDataProtector protector;

    public PersonalDataProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector(typeof(PersonalDataProtector).FullName!);
    }

    public string? Protect(string? data)
    {
        return data is null ? null : protector.Protect(data);
    }

    public string? Unprotect(string? data)
    {
        return data is null ? null : protector.Unprotect(data);
    }
}
