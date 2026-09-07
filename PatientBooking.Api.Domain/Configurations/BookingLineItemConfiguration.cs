using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public class BookingLineItemConfiguration : IEntityTypeConfiguration<BookingLineItem>
{
    public void Configure(EntityTypeBuilder<BookingLineItem> builder)
    {
        builder.Property(l => l.Price).HasColumnType("decimal(18,2)");
    }
}
