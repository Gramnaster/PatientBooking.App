using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public static class AdminBootstrapper
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        PatientBookingDbContext db,
        AdminSeedSettings settings,
        TimeProvider clock,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(settings.Email))
        {
            if (!string.IsNullOrEmpty(settings.Password))
                throw new InvalidOperationException(
                    "AdminSeed:Email is required when a bootstrap password is supplied."
                );

            return;
        }

        // Serialize bootstrap decisions; all account/profile writes must commit together.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await userManager.FindByEmailAsync(settings.Email.Trim());
        var adminUserIds = await db.Admins.Select(a => a.UserId).ToListAsync(ct);

        if (adminUserIds.Count > 0)
        {
            if (user is null || adminUserIds.Count != 1 || !string.Equals(adminUserIds[0], user.Id, StringComparison.Ordinal) || user.DeletedAtUtc is not null)
            {
                throw new InvalidOperationException(
                    "Admin bootstrap refused: an admin other than the configured active account exists."
                );
            }

            // Never reset an existing admin's password from deployment configuration.
            await transaction.CommitAsync(ct);
            return;
        }

        if (user is not null)
        {
            throw new InvalidOperationException(
                "Admin bootstrap refused: the configured email already belongs to a non-admin account. Use an unused email."
            );
        }

        if (
            string.IsNullOrWhiteSpace(settings.Password) || string.IsNullOrWhiteSpace(
                settings.FirstName
            ) || string.IsNullOrWhiteSpace(settings.LastName)
        )
        {
            throw new InvalidOperationException(
                "Creating the initial admin requires AdminSeed:Password, FirstName and LastName."
            );
        }

        user = new ApplicationUser
        {
            Email = settings.Email.Trim(),
            UserName = settings.Email.Trim(),
            FirstName = settings.FirstName.Trim(),
            LastName = settings.LastName.Trim(),
            // The deployment operator provisions this account; no SMTP dependency at startup.
            EmailConfirmed = true,
            CreatedAtUtc = clock.GetUtcNow(),
        };

        var result = await userManager.CreateAsync(user, settings.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Admin account creation failed. Identity error codes: " + string.Join(
                    ", ",
                    result.Errors.Select(e => e.Code)
                )
            );
        }

        var patient = new Patient { UserId = user.Id, CreatedAtUtc = clock.GetUtcNow() };
        var admin = new Admin { UserId = user.Id, CreatedAtUtc = clock.GetUtcNow() };
        db.Patients.Add(patient);
        db.Admins.Add(admin);
        await db.SaveChangesAsync(ct);

        patient.MedicalRecordNumber = IdentifierCodeEncoder.Encode(
            patient.Id,
            IdentifierCodeEncoder.MedicalRecordNumberShape
        );
        admin.AdminNumber = IdentifierCodeEncoder.Encode(admin.Id, IdentifierCodeEncoder.AdminNumberShape);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
