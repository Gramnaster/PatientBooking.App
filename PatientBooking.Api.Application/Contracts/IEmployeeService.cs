using System;
using System.Collections.Generic;
using System.Text;
using PatientBooking.Api.Application.DTOs.Employee;
using PatientBooking.Api.Common.Results;

namespace PatientBooking.Api.Application.Contracts;

public interface IEmployeeService
{
    Task<Result<IReadOnlyList<GetEmployeeDto>>> GetEmployeesAsync(CancellationToken ct);
    Task<Result<GetEmployeeDto>> GetEmployeeAsync(int id, CancellationToken ct);
    Task<Result<GetEmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto createDto, CancellationToken ct);
    Task<Result> UpdateEmployeeAsync(int id, UpdateEmployeeDto updateDto, CancellationToken ct);
    Task<Result> DeleteEmployeeAsync(int id, CancellationToken ct);
}
