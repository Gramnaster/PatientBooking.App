using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Domain;

public class Patient
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string MedicalRecordNumber { get; set; } = string.Empty;
    public int? RegisteredAtClinicId { get; set; }
    public Clinic? RegisteredAtClinic { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

}
