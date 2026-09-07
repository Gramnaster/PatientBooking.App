using System;
using System.Collections.Generic;
using System.Text;
using PatientBooking.Api.Application.DTOs.Booking;
using PatientBooking.Api.Common.Results;

namespace PatientBooking.Api.Application.Contracts;

public interface IBookingService
{
    Task<Result<GetBookingDto>> CreateBookingAsync(CreateBookingDto dto, string idempotencyKey, CancellationToken ct);
    Task<Result<GetBookingDto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<IReadOnlyList<GetBookingDto>>> GetMyBookingsAsync(CancellationToken ct);
    Task<Result> CancelBookingAsync(int id, CancellationToken ct);

    // Employee only below
    Task<Result<GetBookingDto>> CreateForClinicAsync(
        int clinicId,
        CreateBookingForPatientDto dto,
        string idempotencyKey,
        CancellationToken ct
    );

    Task<Result<GetBookingDto>> GetByIdForClinicAsync(int clinicId, int bookingId, CancellationToken ct);
    Task<Result<IReadOnlyList<GetBookingDto>>> ListBookingsByClinicAsync(int clinicId, CancellationToken ct);
    Task<Result> CancelForClinicAsync(int clinicId, int bookingId, CancellationToken ct);
}
