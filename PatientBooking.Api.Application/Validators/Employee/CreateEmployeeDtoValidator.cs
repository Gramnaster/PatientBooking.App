using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using PatientBooking.Api.Application.DTOs.Employee;

namespace PatientBooking.Api.Application.Validators.Employee;

public sealed class CreateEmployeeDtoValidator : AbstractValidator<CreateEmployeeDto>
{
    public CreateEmployeeDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ClinicId).GreaterThan(0);
    }
}
