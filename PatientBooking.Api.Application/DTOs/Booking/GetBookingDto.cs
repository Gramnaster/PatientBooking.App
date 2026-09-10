using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Booking;

/// <param name="Id">
/// The booking's own identifier. Pass this to <c>GET /api/Booking/{id}</c> and
/// <c>DELETE /api/Booking/{id}</c> - not <paramref name="BookingNumber"/>, which resets per clinic
/// per day and is not globally unique, and not <paramref name="PatientId"/>, which identifies the
/// patient rather than the booking.
/// </param>
/// <param name="BookingNumber">Human-readable label (e.g. "A001"). Unique only within its clinic and day.</param>
/// <param name="PatientId">The owning patient's identifier - not the booking's.</param>
public sealed record GetBookingDto(
    int Id,
    string BookingNumber,
    DateTimeOffset AppointmentStartUtc,
    ICollection<GetBookingLineItemDto> GetBookingLineItemsDto,
    decimal TotalPrice,
    string MedicalRecordNumber,
    int PatientId,
    string PatientFullName,
    string ClinicName
);
