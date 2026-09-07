using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using PatientBooking.Api.Application.DTOs.Booking;

namespace PatientBooking.Api.Application.Validators.Booking;

public sealed class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingDtoValidator(TimeProvider clock)
    {
        RuleFor(x => x.ClinicId).GreaterThan(0);

        // Obviously prevent past bookings
        RuleFor(x => x.AppointmentStartUtc)
            .Must(v => v > clock.GetUtcNow())
            .WithMessage("AppointmentStartUtc must be in the future.");

        // Half-hour slots. Anything else is no-no.
        RuleFor(x => x.AppointmentStartUtc)
            .Must(v => v is { Minute: 0 or 30, Second: 0, Millisecond: 0 })
            .WithMessage("AppointmentStartUtc must fall on a half-hour boundary (:00 or :30).");
    }
}
