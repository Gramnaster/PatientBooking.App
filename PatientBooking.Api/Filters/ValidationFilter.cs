using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PatientBooking.Api.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        List<ValidationFailure> failures = [];

        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            Type validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            ValidationContext<object> validationContext = new(argument);
            ValidationResult result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted
            );
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            var errors = failures.GroupBy(f => f.PropertyName, StringComparer.Ordinal).ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal
            );

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
            });

            return;
        }

        await next();
    }
}
