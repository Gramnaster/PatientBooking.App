using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Booking;

public sealed record GetBookingDto(
    string BookingNumber,
    DateTimeOffset AppointmentStartUtc,
    ICollection<GetBookingLineItemDto> GetBookingLineItemsDto,
    decimal TotalPrice,
    string MedicalRecordNumber,
    int PatientId,
    string PatientFullName,
    string ClinicName
);
