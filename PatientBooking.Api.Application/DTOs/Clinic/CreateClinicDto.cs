namespace PatientBooking.Api.Application.DTOs.Clinic;

public record CreateClinicDto
{
    public required string Name { get; set; }
    public required string Address { get; set; }
}
