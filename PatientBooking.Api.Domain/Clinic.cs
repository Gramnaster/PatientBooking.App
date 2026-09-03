using System.ComponentModel.DataAnnotations.Schema;

namespace PatientBooking.Api.Domain;

public class Clinic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    [NotMapped]
    public int EmployeeCount { get; set; }
    public ICollection<Employee> Employees { get; set; } = [];
    public ICollection<Patient> Patients { get; set; } = [];

    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
