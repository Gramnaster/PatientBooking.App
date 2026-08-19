using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PatientBooking.Api.AuthorizationFilters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class AdminOrEmployeeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpUser = context.HttpContext.User;

        if (httpUser is not { Identity.IsAuthenticated: true })
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (httpUser.IsInRole("Admin"))
        {
            return;
        }

        if (!context.RouteData.Values.TryGetValue("clinicId", out var clinicIdObj)
            || !string.Equals(
                    httpUser.FindFirst("ClinicId")?.Value,
                    clinicIdObj?.ToString(),
                    StringComparison.Ordinal)
        )
        {
            context.Result = new ForbidResult();
        }
    }
}
