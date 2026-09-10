using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Domain.Configurations;

namespace PatientBooking.Api.Domain;

public class PatientBookingDbContext(
    DbContextOptions<PatientBookingDbContext> options,
    IPersonalDataProtector personalDataProtector,
    ILookupProtector lookupProtector
) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Admin> Admins { get; set; } = null!;
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Clinic> Clinics { get; set; } = null!;
    public DbSet<ClinicOperatingHours> ClinicOperatingHours { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    public DbSet<Booking> Bookings { get; set; } = null!;
    public DbSet<BookingLineItem> BookingLineItems { get; set; } = null!;
    public DbSet<EmailOutboxMessage> EmailOutboxMessages { get; set; } = null!;
    public DbSet<SentEmailNotification> SentEmailNotifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly(),
            type => type != typeof(ApplicationUserConfiguration)
        );

        builder.ApplyConfiguration(new ApplicationUserConfiguration(personalDataProtector, lookupProtector));
    }
}
