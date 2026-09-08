using FluentValidation;
using PatientBooking.Api.Application.DTOs.Employee;

namespace PatientBooking.Api.Application.Validators.Employee;

public sealed class UpdateEmployeeDtoValidator : AbstractValidator<UpdateEmployeeDto>
{
    public UpdateEmployeeDtoValidator()
    {
        RuleFor(x => x.ClinicId).GreaterThan(0);
    }
}
