using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        // Limited since it's a tiny group
        builder.Property(a => a.AdminNumber).HasMaxLength(10);

        // Unassigned Admin has no AdminNumber between the two SaveChangesAsync call
        builder.HasIndex(a => a.AdminNumber).IsUnique().HasFilter("[AdminNumber] IS NOT NULL");
    }
}
