using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Domain;

public class Employee
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string? EmployeeNumber { get; set; }
    public int? ClinicId { get; set; }
    public Clinic? Clinic { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
