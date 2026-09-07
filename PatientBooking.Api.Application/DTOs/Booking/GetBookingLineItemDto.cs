using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using PatientBooking.Api.Common.Enums;

namespace PatientBooking.Api.Application.DTOs.Booking;

public sealed record GetBookingLineItemDto(decimal Price, LineItemType LineItemType);
