namespace PatientBooking.Api.Application.DTOs.Clinic;

public sealed record UpdateClinicDto
{
    public required string Name { get; set; }
    public required string Address { get; set; }
}
