using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PatientBooking.Api.Domain.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Domain.Configurations;

public sealed class ApplicationUserConfiguration(IPersonalDataProtector personalDataProtector, ILookupProtector lookupProtector) : IEntityTypeConfiguration<ApplicationUser>
{
    private const int ProtectedColumnMaxLength = 500;

    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        var nullablePersonalDataConverter = new ValueConverter<string?, string?>(
    v => v == null ? null : personalDataProtector.Protect(v),
    v => v == null ? null : personalDataProtector.Unprotect(v));

        var personalDataConverter = new ValueConverter<string, string>(
            v => personalDataProtector.Protect(v)!,
            v => personalDataProtector.Unprotect(v)!);

        var lookupConverter = new ValueConverter<string?, string?>(
            v => v == null ? null : lookupProtector.Protect(LookupProtectorKeyRing.DefaultKeyId, v),
            v => v);

        builder.Property(u => u.UserName).HasConversion(nullablePersonalDataConverter).HasMaxLength(ProtectedColumnMaxLength);
        builder.Property(u => u.Email).HasConversion(nullablePersonalDataConverter).HasMaxLength(ProtectedColumnMaxLength);
        builder.Property(u => u.PhoneNumber).HasConversion(nullablePersonalDataConverter).HasMaxLength(ProtectedColumnMaxLength);
        builder.Property(u => u.FirstName).HasConversion(personalDataConverter).HasMaxLength(ProtectedColumnMaxLength);
        builder.Property(u => u.LastName).HasConversion(personalDataConverter).HasMaxLength(ProtectedColumnMaxLength);

        builder.Property(u => u.NormalizedUserName).HasConversion(lookupConverter);
        builder.Property(u => u.NormalizedEmail).HasConversion(lookupConverter);
    }
}
