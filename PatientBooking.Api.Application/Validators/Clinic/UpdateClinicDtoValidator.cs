using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using PatientBooking.Api.Application.DTOs.Clinic;

namespace PatientBooking.Api.Application.Validators.Clinic;

public sealed class UpdateClinicDtoValidator : AbstractValidator<UpdateClinicDto>
{
    public UpdateClinicDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(255);
    }
}
