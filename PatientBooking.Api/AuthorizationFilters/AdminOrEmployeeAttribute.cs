using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.AuthorizationFilters;

// DB lookup, not a JWT claim - stays correct if an employee's clinic assignment changes.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class AdminOrEmployeeAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
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

        if (!httpUser.IsInRole("Employee"))
        {
            context.Result = new ForbidResult();
            return;
        }

        if (
            !context.RouteData.Values.TryGetValue("clinicId", out var clinicIdObj) ||
            !int.TryParse(clinicIdObj?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int clinicId)
        )
        {
            context.Result = new ForbidResult();
            return;
        }

        string? userId = httpUser.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? httpUser.FindFirst(
            ClaimTypes.NameIdentifier
        )?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<PatientBookingDbContext>();

        // Excludes soft-deleted Employees - removal from a clinic's roster takes effect immediately.
        bool isAssignedToClinic = await dbContext.Employees.AnyAsync(
            e => e.UserId == userId && e.ClinicId == clinicId && e.DeletedAtUtc == null,
            context.HttpContext.RequestAborted
        );

        if (!isAssignedToClinic)
        {
            context.Result = new ForbidResult();
        }
    }
}
