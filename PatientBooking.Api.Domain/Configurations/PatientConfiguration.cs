using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder
            .HasOne(patient => patient.User)
            .WithOne()
            .HasForeignKey<Patient>(patient => patient.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // One Patient profile for one ApplicationUser
        builder.HasIndex(patient => patient.UserId).IsUnique();

        // Unregistered patient has no MRN. Assigned MRNs must be unique.
        builder.HasIndex(x => x.MedicalRecordNumber).IsUnique().HasFilter("[MedicalRecordNumber] IS NOT NULL");
    }
}
