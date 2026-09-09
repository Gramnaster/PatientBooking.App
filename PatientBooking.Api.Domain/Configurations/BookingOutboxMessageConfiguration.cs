using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public class BookingOutboxMessageConfiguration : IEntityTypeConfiguration<BookingOutboxMessage>
{
    public void Configure(EntityTypeBuilder<BookingOutboxMessage> builder)
    {
        builder.HasIndex(m => m.DispatchedAtUtc);

        builder.HasOne(m => m.Booking).WithMany().HasForeignKey(m => m.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
