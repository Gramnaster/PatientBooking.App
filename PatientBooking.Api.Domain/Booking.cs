using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PatientBooking.Api.Domain;

public class Booking
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string BookingNumber { get; set; } = string.Empty;
    public bool FirstTimeBooking { get; set; }

    public DateTimeOffset AppointmentStartUtc { get; set; }
    public DateOnly AppointmentDateUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    public ICollection<BookingLineItem> LineItems { get; set; } = [];

    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
