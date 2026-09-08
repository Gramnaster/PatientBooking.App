using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Employee;

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeesController(IEmployeeService employeeService) : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<GetEmployeeDto>>> GetEmployeesAsync(CancellationToken ct)
    {
        var result = await employeeService.GetEmployeesAsync(ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GetEmployeeDto>> GetEmployeeAsync(int id, CancellationToken ct)
    {
        var result = await employeeService.GetEmployeeAsync(id, ct);
        return ToActionResult(result);
    }

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

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEmployeeAsync(int id, UpdateEmployeeDto updateDto, CancellationToken ct)
    {
        var result = await employeeService.UpdateEmployeeAsync(id, updateDto, ct);
        return ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployeeAsync(int id, CancellationToken ct)
    {
        var result = await employeeService.DeleteEmployeeAsync(id, ct);
        return ToActionResult(result);
    }
}
