using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Employee;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public sealed class EmployeeService(
    UserManager<ApplicationUser> userManager,
    PatientBookingDbContext patientBookingDbContext
) : IEmployeeService
{
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
        Employee employee = new() { UserId = user.Id, ClinicId = createDto.ClinicId, };
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
            await userManager.DeleteAsync(user);
            return Result<GetEmployeeDto>.Conflict("Could not create an employee profile. Please try again.");
        }

        GetEmployeeDto getEmployeeDto = new()
        {
            Id = employee.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmployeeNumber = employee.EmployeeNumber,
            ClinicId = employee.ClinicId!.Value,
        };

        return Result<GetEmployeeDto>.Success(getEmployeeDto);
    }
}
