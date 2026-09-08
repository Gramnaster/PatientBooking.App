using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Application.DTOs.Employee;

public record GetEmployeeDto
{
    public required int Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string EmployeeNumber { get; set; }
    public required int ClinicId { get; set; }
}
