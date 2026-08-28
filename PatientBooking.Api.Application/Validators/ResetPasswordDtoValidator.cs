using FluentValidation;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.Validators;

public sealed class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator(IBreachedPasswordChecker breachedPasswordChecker)
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one non-alphanumeric character.")
            .MustAsync(async (password, ct) => !await breachedPasswordChecker.IsBreachedAsync(password, ct))
            .WithMessage("This password has appeared in a known data breach. Please choose a different password.");
    }
}
