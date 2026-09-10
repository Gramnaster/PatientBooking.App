using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public class RegistrationOutboxMessageConfiguration : IEntityTypeConfiguration<RegistrationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<RegistrationOutboxMessage> builder)
    {
        builder.HasIndex(m => m.DispatchedAtUtc);

        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
