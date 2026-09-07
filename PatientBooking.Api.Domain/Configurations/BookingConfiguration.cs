using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PatientBooking.Api.Domain.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasOne(b => b.Clinic).WithMany().HasForeignKey(b => b.ClinicId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Patient).WithMany().HasForeignKey(b => b.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(b => b.LineItems)
            .WithOne(l => l.Booking)
            .HasForeignKey(l => l.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // BookingNumber uniqueness resets per clinic per day - protects ANNN sequence
        // Scoped to active rows so cancelled booking's number isn't held forever
        builder
            .HasIndex(b => new { b.ClinicId, b.AppointmentDateUtc, b.BookingNumber, })
            .IsUnique()
            .HasFilter("[DeletedAtUtc] IS NULL");

        // Index that stops two patients booking the same clinic slot
        // Scoped to active row so cancelling a booking (soft-delete) frees its slot for a new one
        builder
            .HasIndex(b => new { b.ClinicId, b.AppointmentStartUtc, })
            .IsUnique()
            .HasFilter("[DeletedAtUtc] IS NULL");

        // Scoped to PatientId, IdempotencyKey
        // Header dedupes one caller's own retries, not literal string value across every patient
        builder.HasIndex(b => new { b.PatientId, b.IdempotencyKey, }).IsUnique();

        // One FirstTimeBooking = 1 row per (PatientId, ClinicId)
        // DB-level backstop for surcharge in BookingServices
        // In case two requests for a patient's first visit to the same clinic land at same instant
        builder
            .HasIndex(b => new { b.PatientId, b.ClinicId, })
            .IsUnique()
            .HasFilter("[FirstTimeBooking] = 1 AND [DeletedAtUtc] IS NULL");
    }
}
