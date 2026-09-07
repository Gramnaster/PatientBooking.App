using System;
using System.Collections.Generic;
using System.Text;
using PatientBooking.Api.Application.DTOs.Booking;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Mappers;

internal static class BookingMapper
{
    public static IQueryable<GetBookingDto> ProjectToGetBookingDto(this IQueryable<Booking> query) =>
        query.Select(
            b => new GetBookingDto(
                b.BookingNumber,
                b.AppointmentStartUtc,
                b.LineItems.Select(l => new GetBookingLineItemDto(l.Price, l.LineItemType)).ToList(),
                b.LineItems.Sum(l => l.Price),
                b.Patient!.MedicalRecordNumber ?? string.Empty,
                b.PatientId,
                b.Patient!.User!.LastName + ", " + b.Patient.User.FirstName,
                b.Clinic!.Name
            )
        );
}
