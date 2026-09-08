using PatientBooking.Api.Application.DTOs.Employee;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Mappers;

internal static class EmployeeMapper
{
    public static IQueryable<GetEmployeeDto> ProjectToGetEmployeeDto(this IQueryable<Employee> query) =>
        query.Select(
            e => new GetEmployeeDto
            {
                Id = e.Id,
                Email = e.User!.Email!,
                FirstName = e.User.FirstName,
                LastName = e.User.LastName,
                EmployeeNumber = e.EmployeeNumber ?? string.Empty,
                ClinicId = e.ClinicId!.Value,
            }
        );
}
