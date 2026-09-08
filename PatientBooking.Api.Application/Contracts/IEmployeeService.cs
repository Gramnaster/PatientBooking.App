using System;
using System.Collections.Generic;
using System.Text;
using PatientBooking.Api.Application.DTOs.Employee;
using PatientBooking.Api.Common.Results;

namespace PatientBooking.Api.Application.Contracts;

public interface IEmployeeService
{
    Task<Result<GetEmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto createDto, CancellationToken ct);
}
