using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using PatientBooking.Api.Application.DTOs.Booking;

namespace PatientBooking.Api.Application.Validators.Booking;

public sealed class CreateBookingForPatientDtoValidator : AbstractValidator<CreateBookingForPatientDto>
{
    public CreateBookingForPatientDtoValidator(TimeProvider clock)
    {
        RuleFor(x => x.PatientId).GreaterThan(0);

        // Can't book in the past
        RuleFor(x => x.AppointmentStartUtc)
            .Must(v => v > clock.GetUtcNow())
            .WithMessage("AppointmentStartUtc must be in the future.");

        // Can only book on :00 and :30
        RuleFor(x => x.AppointmentStartUtc)
            .Must(v => v is { Minute: 0 or 30, Second: 0, Millisecond: 0 })
            .WithMessage("AppointmentStartUtc must fall on a half-hour boundary (:00 or :30).");
    }
}
