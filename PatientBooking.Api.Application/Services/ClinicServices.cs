using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Clinic;
using PatientBooking.Api.Application.Mappers;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public class ClinicServices(PatientBookingDbContext patientBookingDbContext, TimeProvider clock) : IClinicService
{
    public async Task<Result<IReadOnlyList<GetClinicDto>>> GetClinicsAsync(CancellationToken ct)
    {
        List<GetClinicDto> query = await patientBookingDbContext
            .Clinics
            .AsNoTracking()
            .Where(c => c.DeletedAtUtc == null)
            .Select(c => new GetClinicDto(c.Id, c.Name, c.Address))
            .ToListAsync(ct);

        return Result<IReadOnlyList<GetClinicDto>>.Success(query);
    }

    public async Task<Result<GetClinicDto>> GetClinicAsync(int clinicId, CancellationToken ct)
    {
        GetClinicDto? clinic = await patientBookingDbContext
            .Clinics
            .AsNoTracking()
            .Where(c => c.Id == clinicId && c.DeletedAtUtc == null)
            .ProjectToGetClinicDto()
            .FirstOrDefaultAsync(ct);

        if (clinic is null)
        {
            return Result<GetClinicDto>.NotFound(
                string.Create(CultureInfo.InvariantCulture, $"Clinic {clinicId} not found.")
            );
        }

        return Result<GetClinicDto>.Success(clinic);
    }

    public async Task<Result<GetClinicDto>> CreateClinicAsync(CreateClinicDto createDto, CancellationToken ct)
    {
        // 1. Check if it exists already.
        if (await ClinicExistsAsync(createDto.Name, excludingId: null, ct))
        {
            return Result<GetClinicDto>.Failure(
                new ResultError(nameof(ErrorCodes.Conflict), $"Clinic with name '{createDto.Name}' already exists.")
            );
        }

        // 2. Map DTO to entity
        var clinic = ClinicMapper.ToClinic(createDto);

        // 3. Fill in what the DTO can't know
        clinic.CreatedAtUtc = clock.GetUtcNow();

        // Fresh clinic starts with full week: Mon to Sat 0900-1700, closed Sunday.
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            bool isWeekday = day is not (DayOfWeek.Sunday);
            clinic.OperatingHours.Add(new ClinicOperatingHours
            {
                DayOfWeek = day,
                OpenTime = isWeekday ? new TimeOnly(6, 0) : null,
                CloseTime = isWeekday ? new TimeOnly(22, 0) : null,
            });
        }

        // 4. Stage the insert
        await patientBookingDbContext.AddAsync(clinic, ct);

        // 5. Commit to DB
        try
        {
            await patientBookingDbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<GetClinicDto>.Conflict($"Clinic with name '{createDto.Name}' already exists.");
        }

        // 6. Map back to response DTO
        var dto = ClinicMapper.ToGetClinicDto(clinic);

        return Result<GetClinicDto>.Success(dto);
    }

    public async Task<Result> UpdateClinicAsync(int id, UpdateClinicDto updateDto, CancellationToken ct)
    {
        // Load entity to track -> Check if found
        Clinic? clinic = await patientBookingDbContext.Clinics.FirstOrDefaultAsync(
            c => c.Id == id && c.DeletedAtUtc == null,
            ct
        );

        if (clinic is null)
        {
            return Result.NotFound(string.Create(CultureInfo.InvariantCulture, $"Clinic {id} not found."));
        }

        if (await ClinicExistsAsync(updateDto.Name, excludingId: id, ct))
        {
            return Result.Conflict($"Clinic with name '{updateDto.Name}' already exists.");
        }

        ClinicMapper.UpdateClinic(updateDto, clinic);
        clinic.UpdatedAtUtc = clock.GetUtcNow();

        try
        {
            await patientBookingDbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Conflict($"Clinic with name '{updateDto.Name}' already exists.");
        }

        return Result.Success();
    }

    public async Task<Result> DeleteClinicAsync(int id, CancellationToken ct)
    {
        Clinic? clinic = await patientBookingDbContext.Clinics.FirstOrDefaultAsync(
            c => c.Id == id && c.DeletedAtUtc == null,
            ct
        );

        if (clinic is null)
        {
            return Result.NotFound(string.Create(CultureInfo.InvariantCulture, $"Clinic {id} not found."));
        }

        clinic.UpdatedAtUtc = clock.GetUtcNow();
        clinic.DeletedAtUtc = clock.GetUtcNow();
        await patientBookingDbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
    public async Task<Result<IReadOnlyList<GetClinicOperatingHoursDto>>> GetOperatingHoursAsync(
        int clinicId,
        CancellationToken ct
    )
    {
        bool clinicExists = await patientBookingDbContext.Clinics.AnyAsync(
            c => c.Id == clinicId && c.DeletedAtUtc == null,
            ct
        );

        if (!clinicExists)
        {
            return Result<IReadOnlyList<GetClinicOperatingHoursDto>>.NotFound(
                string.Create(CultureInfo.InvariantCulture, $"Clinic {clinicId} not found.")
            );
        }

        List<GetClinicOperatingHoursDto> hours = await patientBookingDbContext
            .ClinicOperatingHours
            .AsNoTracking()
            .Where(h => h.ClinicId == clinicId)
            .Select(h => new GetClinicOperatingHoursDto(h.DayOfWeek, h.OpenTime, h.CloseTime))
            .ToListAsync(ct);

        // Sort by the enum's value instead of string for proper sorting
        hours.Sort((a, b) => a.DayOfWeek.CompareTo(b.DayOfWeek));

        return Result<IReadOnlyList<GetClinicOperatingHoursDto>>.Success(hours);
    }
    public async Task<Result> UpdateOperatingHoursAsync(
        int clinicId,
        IReadOnlyList<UpdateClinicOperatingHoursDto> days,
        CancellationToken ct
    )
    {
        Clinic? clinic = await patientBookingDbContext
            .Clinics
            .Include(c => c.OperatingHours)
            .FirstOrDefaultAsync(c => c.Id == clinicId && c.DeletedAtUtc == null, ct);

        if (clinic is null)
        {
            return Result.NotFound(string.Create(CultureInfo.InvariantCulture, $"Clinic {clinicId} not found."));
        }

        // A PUT replaces the whole week - exactly one entry per DayOfWeek value, no gaps and no duplicates
        if (days.Count != 7 || days.Select(d => d.DayOfWeek).Distinct().Take(8).Count() != 7)
        {
            return Result.BadRequest(
                new ResultError(nameof(ErrorCodes.BadRequest), "Exactly one entry per day of the week is required.")
            );
        }

        foreach (UpdateClinicOperatingHoursDto day in days)
        {
            if ((day.OpenTime is null) != (day.CloseTime is null))
            {
                return Result.BadRequest(
                    new ResultError(
                        nameof(ErrorCodes.BadRequest),
                        $"{day.DayOfWeek} OpenTime and CloseTime must both be set or both be null."
                    )
                );
            }

            if (day.OpenTime is not null && day.OpenTime >= day.CloseTime)
            {
                return Result.BadRequest(
                    new ResultError(
                        nameof(ErrorCodes.BadRequest),
                        $"{day.DayOfWeek}: OpenTime must be before CloseTime."
                    )
                );
            }
        }

        foreach (ClinicOperatingHours existing in clinic.OperatingHours)
        {
            UpdateClinicOperatingHoursDto match = days.First(d => d.DayOfWeek == existing.DayOfWeek);

            existing.OpenTime = match.OpenTime;
            existing.CloseTime = match.CloseTime;
        }

        await patientBookingDbContext.SaveChangesAsync(ct);

        return Result.Success();
    }

    private Task<bool> ClinicExistsAsync(string name, int? excludingId, CancellationToken ct)
    {
        return patientBookingDbContext
            .Clinics
            .Where(c => c.DeletedAtUtc == null && (excludingId == null || c.Id != excludingId))
            .AnyAsync(c => c.Name.Trim() == name.Trim(), ct);
    }
}
