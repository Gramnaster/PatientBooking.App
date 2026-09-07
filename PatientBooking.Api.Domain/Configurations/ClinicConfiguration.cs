using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.Property(c => c.Name).UseCollation("SQL_Latin1_General_CP1_CI_AS");

        builder.HasIndex(c => c.Name).IsUnique().HasFilter("[DeletedAtUtc] IS NULL");
    }
}
