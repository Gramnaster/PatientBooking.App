using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.EmployeeNumber).HasMaxLength(24);

        builder.HasIndex(e => e.EmployeeNumber).IsUnique().HasFilter("[EmployeeNumber] IS NOT NULL");
    }
}
