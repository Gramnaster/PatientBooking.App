using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Employee;

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeesController(IEmployeeService employeeService) : BaseApiController
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GetEmployeeDto>> CreateEmployeeAsync(
        CreateEmployeeDto createDto,
        CancellationToken ct
    )
    {
        var result = await employeeService.CreateEmployeeAsync(createDto, ct);
        return ToActionResult(result);
    }
}
