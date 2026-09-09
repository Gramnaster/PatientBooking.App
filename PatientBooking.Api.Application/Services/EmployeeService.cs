using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Employee;
using PatientBooking.Api.Application.Mappers;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public sealed class EmployeeService(
    UserManager<ApplicationUser> userManager,
    PatientBookingDbContext patientBookingDbContext,
    TimeProvider clock
) : IEmployeeService
{
    public async Task<Result<IReadOnlyList<GetEmployeeDto>>> GetEmployeesAsync(CancellationToken ct)
    {
        List<GetEmployeeDto> employees = await patientBookingDbContext
            .Employees
            .AsNoTracking()
            .Where(e => e.DeletedAtUtc == null)
            .ProjectToGetEmployeeDto()
            .ToListAsync(ct);

        return Result<IReadOnlyList<GetEmployeeDto>>.Success(employees);
    }

    public async Task<Result<GetEmployeeDto>> GetEmployeeAsync(int id, CancellationToken ct)
    {
        GetEmployeeDto? employee = await patientBookingDbContext
            .Employees
            .AsNoTracking()
            .Where(e => e.Id == id && e.DeletedAtUtc == null)
            .ProjectToGetEmployeeDto()
            .FirstOrDefaultAsync(ct);

        if (employee is null)
        {
            return Result<GetEmployeeDto>.NotFound(
                string.Create(CultureInfo.InvariantCulture, $"Employee {id} not found.")
            );
        }

        return Result<GetEmployeeDto>.Success(employee);
    }

    public async Task<Result<GetEmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto createDto, CancellationToken ct)
    {
        bool clinicExists = await patientBookingDbContext.Clinics.AnyAsync(
            c => c.Id == createDto.ClinicId && c.DeletedAtUtc == null,
            ct
        );

        if (!clinicExists)
        {
            return Result<GetEmployeeDto>.NotFound("Clinic not found.");
        }

        ApplicationUser user = new()
        {
            Email = createDto.Email,
            UserName = createDto.Email,
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            // No need to click email confirmation for employees
            EmailConfirmed = true,
            CreatedAtUtc = clock.GetUtcNow(),
        };

        await using var transaction = await patientBookingDbContext.Database.BeginTransactionAsync(ct);

        IdentityResult createResult = await userManager.CreateAsync(user, createDto.Password);

        if (!createResult.Succeeded)
        {
            var registrationErrors = createResult
                .Errors
                .Select(e => new ResultError(nameof(ErrorCodes.BadRequest), e.Description))
                .ToArray();

            return Result<GetEmployeeDto>.BadRequest(registrationErrors);
        }

        // EmployeeNumber needs employee.Id, which only exists once row has been saved
        Employee employee = new() { UserId = user.Id, ClinicId = createDto.ClinicId, CreatedAtUtc = clock.GetUtcNow() };
        patientBookingDbContext.Add(employee);

        try
        {
            await patientBookingDbContext.SaveChangesAsync(ct);

            employee.EmployeeNumber = IdentifierCodeEncoder.Encode(
                employee.Id,
                IdentifierCodeEncoder.EmployeeNumberShape
            );
            await patientBookingDbContext.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<GetEmployeeDto>.Conflict("Could not create an employee profile. Please try again.");
        }

        GetEmployeeDto getEmployeeDto = new()
        {
            Id = employee.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmployeeNumber = employee.EmployeeNumber,
            ClinicId = employee.ClinicId.Value,
        };

        return Result<GetEmployeeDto>.Success(getEmployeeDto);
    }

    public async Task<Result> UpdateEmployeeAsync(int id, UpdateEmployeeDto updateDto, CancellationToken ct)
    {
        Employee? employee = await patientBookingDbContext.Employees.FirstOrDefaultAsync(
            e => e.Id == id && e.DeletedAtUtc == null,
            ct
        );

        if (employee is null)
        {
            return Result.NotFound(string.Create(CultureInfo.InvariantCulture, $"Employee {id} not found."));
        }

        bool clinicExists = await patientBookingDbContext.Clinics.AnyAsync(
            c => c.Id == updateDto.ClinicId && c.DeletedAtUtc == null,
            ct
        );

        if (!clinicExists)
        {
            return Result.NotFound("Clinic not found.");
        }

        employee.ClinicId = updateDto.ClinicId;
        employee.UpdatedAtUtc = clock.GetUtcNow();

        await patientBookingDbContext.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeleteEmployeeAsync(int id, CancellationToken ct)
    {
        Employee? employee = await patientBookingDbContext.Employees.FirstOrDefaultAsync(
            e => e.Id == id && e.DeletedAtUtc == null,
            ct
        );

        if (employee is null)
        {
            return Result.NotFound(string.Create(CultureInfo.InvariantCulture, $"Employee {id} not found."));
        }

        await using var transaction = await patientBookingDbContext.Database.BeginTransactionAsync(ct);
        var user = await userManager.FindByIdAsync(employee.UserId);
        if (user is null)
            return Result.NotFound("Employee account not found.");

        var now = clock.GetUtcNow();
        user.UpdatedAtUtc = now;
        user.DeletedAtUtc = now;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var updateResult = await userManager.UpdateSecurityStampAsync(user);
        if (!updateResult.Succeeded)
            return Result.Failure(
                updateResult.Errors.Select(e => new ResultError(nameof(ErrorCodes.BadRequest), e.Description)).ToArray()
            );

        var tokens = await patientBookingDbContext
            .RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
            token.RevokedAtUtc = now;

        employee.UpdatedAtUtc = now;
        employee.DeletedAtUtc = now;
        await patientBookingDbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result.Success();
    }
}
