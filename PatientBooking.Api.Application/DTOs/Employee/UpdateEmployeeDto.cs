namespace PatientBooking.Api.Application.DTOs.Employee;

public record UpdateEmployeeDto
{
    public required int ClinicId { get; set; }
}
