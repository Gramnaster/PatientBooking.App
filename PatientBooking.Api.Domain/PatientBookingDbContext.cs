using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Domain.Configurations;
using System.Reflection;

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
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PatientBookingDbContext).Assembly);

        builder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly(),
            type => type != typeof(ApplicationUserConfiguration));

        builder.ApplyConfiguration(
            new ApplicationUserConfiguration(
                personalDataProtector,
                lookupProtector));
    }
}
