using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using MimeKit.Cryptography;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Booking;
using PatientBooking.Api.Application.Mappers;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Services;

public sealed class BookingServices(
    PatientBookingDbContext patientBookingDbContext,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider clock
) : IBookingService
{
    private const decimal StandardFeeAmount = 75.00m;
    private const decimal FirstTimeFeeAmount = 20.00m;
    private const int MaxBookingNumberAttempts = 3;
    private static readonly TimeSpan MinimumCancellationNotice = TimeSpan.FromHours(6);

    private const string ForbidPatientProfile = "No patient profile is associated with the current account.";

    private string CurrentUserId =>
        httpContextAccessor.HttpContext?.User.FindFirst(
            JwtRegisteredClaimNames.Sub
        )?.Value ?? httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    public async Task<Result<IReadOnlyList<GetBookingDto>>> GetMyBookingsAsync(CancellationToken ct)
    {
        Patient? patient = await GetCurrentPatientAsync(ct);
        if (patient is null)
        {
            return Result<IReadOnlyList<GetBookingDto>>.Forbid(ForbidPatientProfile);
        }

        List<GetBookingDto> bookings = await patientBookingDbContext
            .Bookings
            .AsNoTracking()
            .Where(b => b.PatientId == patient.Id && b.DeletedAtUtc == null)
            .OrderByDescending(b => b.AppointmentStartUtc)
            .ProjectToGetBookingDto()
            .ToListAsync(ct);

        return Result<IReadOnlyList<GetBookingDto>>.Success(bookings);
    }

    public async Task<Result<GetBookingDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        Patient? patient = await GetCurrentPatientAsync(ct);
        if (patient is null)
        {
            return Result<GetBookingDto>.Forbid(ForbidPatientProfile);
        }

        // Missing and not-owned both return NotFound. Caller can't tell which.
        GetBookingDto? booking = await patientBookingDbContext
            .Bookings
            .AsNoTracking()
            .Where(b => b.Id == id && b.PatientId == patient.Id && b.DeletedAtUtc == null)
            .ProjectToGetBookingDto()
            .FirstOrDefaultAsync(ct);

        if (booking is null)
        {
            return Result<GetBookingDto>.NotFound(
                string.Create(CultureInfo.InvariantCulture, $"Booking {id} not found.")
            );
        }

        return Result<GetBookingDto>.Success(booking);
    }

    public async Task<Result> CancelBookingAsync(int id, CancellationToken ct)
    {
        Patient? patient = await GetCurrentPatientAsync(ct);
        if (patient is null)
        {
            return Result.Forbid(ForbidPatientProfile);
        }

        Booking? booking = await patientBookingDbContext.Bookings.FirstOrDefaultAsync(
            b => b.Id == id && b.PatientId == patient.Id && b.DeletedAtUtc == null,
            ct
        );

        if (booking is null)
        {
            return Result.NotFound(string.Create(CultureInfo.InvariantCulture, $"Booking {id} not found."));
        }

        // Cancellation needs at least 6 hours' notice. Booking inside that window is not cancellable.
        // Subtraction rejects both cases the same way.
        if (booking.AppointmentStartUtc - clock.GetUtcNow() < MinimumCancellationNotice)
        {
            return Result.Conflict("Bookings can only be cancelled at least 6 hours before the appointment.");
        }

        booking.DeletedAtUtc = clock.GetUtcNow();
        await patientBookingDbContext.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<GetBookingDto>> CreateBookingAsync(
        CreateBookingDto dto,
        string idempotencykey,
        CancellationToken ct
    )
    {
        Patient? patient = await GetCurrentPatientAsync(ct);
        if (patient is null)
        {
            return Result<GetBookingDto>.Forbid(ForbidPatientProfile);
        }

        // Same key -> Same result. Scoped to caller's own PatientId, as guard for request that made it.
        GetBookingDto? existing = await FindByIdempotencyKeyAsync(idempotencykey, patient.Id, ct);
        if (existing is not null)
        {
            return Result<GetBookingDto>.Success(existing);
        }

        if (dto.AppointmentStartUtc <= clock.GetUtcNow())
        {
            return Result<GetBookingDto>.BadRequest(
                new ResultError(nameof(ErrorCodes.BadRequest), "Appointment time must be in the future.")
            );
        }

        // Normalize so DayOfWeek/TimeOfDay below aren't skewed by the client's Offset.
        dto.AppointmentStartUtc = dto.AppointmentStartUtc.ToUniversalTime();

        bool clinicExists = await patientBookingDbContext.Clinics.AnyAsync(
            c => c.Id == dto.ClinicId && c.DeletedAtUtc == null,
            ct
        );
        if (!clinicExists)
        {
            return Result<GetBookingDto>.NotFound(
                string.Create(CultureInfo.InvariantCulture, $"Clinic {dto.ClinicId} not found.")
            );
        }

        // AppointmentStartUtc's DayOfWeek and TimeOfDay are compared vs. clinic's hours
        DayOfWeek appointmentDayOfWeek = dto.AppointmentStartUtc.DayOfWeek;
        var appointmentTimeOfDay = TimeOnly.FromTimeSpan(dto.AppointmentStartUtc.TimeOfDay);

        bool isWithinOperatingHours = await patientBookingDbContext.ClinicOperatingHours.AnyAsync(
            h => h.ClinicId == dto.ClinicId && h.DayOfWeek == appointmentDayOfWeek && h.OpenTime !=
                null && h.CloseTime != null && appointmentTimeOfDay >= h.OpenTime && appointmentTimeOfDay < h.CloseTime,
            ct
        );
        if (!isWithinOperatingHours)
        {
            return Result<GetBookingDto>.Conflict(
                "The clinic is closed at the requested appointment time. Try again the next day."
            );
        }

        // First-time surcharge is scoped per clinic. Patient can be "new patient" at more than one clinic.
        // Redone on every retry attempt so a concurrent request that wins the race is picked ip by the next attempt
        // instead of double-charging the surcharge.
        var appointmentDate = DateOnly.FromDateTime(dto.AppointmentStartUtc.UtcDateTime);

        for (int attempt = 0; attempt < MaxBookingNumberAttempts; attempt++)
        {
            bool isFirstTime =
                !await patientBookingDbContext.Bookings.AnyAsync(
                    b => b.PatientId == patient.Id && b.ClinicId == dto.ClinicId && b.DeletedAtUtc == null,
                    ct
                );

            Booking booking = await BuildBookingAsync(
                dto,
                patient.Id,
                idempotencykey,
                isFirstTime,
                appointmentDate,
                ct
            );
            await patientBookingDbContext.AddAsync(booking, ct);

            try
            {
                await patientBookingDbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (attempt < MaxBookingNumberAttempts - 1)
            {
                // BookNumber race; self-heals - the next loop re-allocates
                // If some slot conflict on (ClinicId, AppointmentStartUtc), goes below instead
                patientBookingDbContext.ChangeTracker.Clear();
                continue;
            }
            catch (DbUpdateException)
            {
                return Result<GetBookingDto>.Conflict("This appointment slot is no longer available.");
            }

            return Result<GetBookingDto>.Success(await ProjectByIdAsync(booking.Id, ct));
        }

        return Result<GetBookingDto>.Conflict("This appointment slot is no longer available.");
    }

    private Task<Patient?> GetCurrentPatientAsync(CancellationToken ct)
    {
        return patientBookingDbContext.Patients.FirstOrDefaultAsync(
            p => p.UserId == CurrentUserId && p.DeletedAtUtc == null,
            ct
        );
    }

    private Task<GetBookingDto?> FindByIdempotencyKeyAsync(string idempotencykey, int id, CancellationToken ct)
    {
        return patientBookingDbContext
            .Bookings
            .AsNoTracking()
            .Where(b => b.IdempotencyKey == idempotencykey && b.PatientId == id)
            .ProjectToGetBookingDto()
            .FirstOrDefaultAsync(ct);
    }

    private Task<GetBookingDto> ProjectByIdAsync(int bookingId, CancellationToken ct)
    {
        return patientBookingDbContext
            .Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .ProjectToGetBookingDto()
            .FirstAsync(ct);
    }

    private async Task<Booking> BuildBookingAsync(
        CreateBookingDto dto,
        int patientId,
        string idempotencyKey,
        bool isFirstTime,
        DateOnly appointmentDate,
        CancellationToken ct
    )
    {
        // 1. Get Booking Number
        string bookingNumber = await AllocateBookingNumberAsync(dto.ClinicId, appointmentDate, ct);
        DateTimeOffset now = clock.GetUtcNow();

        // 2. Get new booking entity
        Booking booking = new()
        {
            ClinicId = dto.ClinicId,
            PatientId = patientId,
            BookingNumber = bookingNumber,
            FirstTimeBooking = isFirstTime,
            AppointmentStartUtc = dto.AppointmentStartUtc,
            AppointmentDateUtc = appointmentDate,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = now,
        };

        // 3. Add it to LineItems collection
        booking.LineItems.Add(new BookingLineItem
        {
            Price = StandardFeeAmount,
            LineItemType = LineItemType.Standard,
            CreatedAtUtc = now,
        });

        // 4. But if first time, give them additional price
        if (isFirstTime)
        {
            booking.LineItems.Add(new BookingLineItem
            {
                Price = FirstTimeFeeAmount,
                LineItemType = LineItemType.FirstTime,
                CreatedAtUtc = now,
            });
        }

        return booking;
    }

    // Gives us triple digit numbers for booking purposes
    private async Task<string> AllocateBookingNumberAsync(int clinicId, DateOnly appointmentDate, CancellationToken ct)
    {
        string? lastNumber = await patientBookingDbContext
            .Bookings
            .Where(b => b.ClinicId == clinicId && b.AppointmentDateUtc == appointmentDate)
            .OrderByDescending(b => b.BookingNumber)
            .Select(b => b.BookingNumber)
            .FirstOrDefaultAsync(ct);

        int sequence = lastNumber is null
            ? 1
            : int.Parse(lastNumber.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture) + 1;

        return string.Create(CultureInfo.InvariantCulture, $"A{sequence:D3}");
    }
}
