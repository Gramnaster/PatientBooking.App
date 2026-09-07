using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public class ClinicOperatingHoursConfiguration : IEntityTypeConfiguration<ClinicOperatingHours>
{
    public void Configure(EntityTypeBuilder<ClinicOperatingHours> builder)
    {
        builder
            .HasOne(h => h.Clinic) // From ClinOH: Each row has one Clinic

            .WithMany(c => c.OperatingHours) // From Clinic: 1-M. Bidirectional nav instead of 1-way FK

            .HasForeignKey(h => h.ClinicId) // Tells EF which scalar column backs relationship

            .OnDelete(DeleteBehavior.Cascade); // If Clinic row is hard-deleted, COH rows delete with it

        builder.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(9);

        // Never more than one roow per day of the week per clinic
        builder.HasIndex(h => new { h.ClinicId, h.DayOfWeek, }).IsUnique();
    }
}
