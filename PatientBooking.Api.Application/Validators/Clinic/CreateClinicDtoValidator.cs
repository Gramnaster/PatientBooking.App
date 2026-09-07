using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using PatientBooking.Api.Application.DTOs.Clinic;

namespace PatientBooking.Api.Application.Validators.Clinic;

// Standard validator to check input
public sealed class CreateClinicDtoValidator : AbstractValidator<CreateClinicDto>
{
    public CreateClinicDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(255);
    }
}
