using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace PatientBooking.Api.Domain.Security;

public sealed class LookupProtector(ILookupProtectorKeyRing keyRing) : ILookupProtector
{
    [return: NotNullIfNotNull(nameof(data))]
    public string? Protect(string keyId, string? data)
    {
        if (data is null) return null;

        var keyBytes = Convert.FromBase64String(keyRing[keyId]);
        using HMACSHA256 hmac = new(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    // One-way by design - nothing needs the plaintext normalized value back
    [return: NotNullIfNotNull(nameof(data))]
    public string? Unprotect(string keyId, string? data)
    {
        throw new NotSupportedException("Lookup-protected values are one-way hashes and cannot be reversed");
    }
}
