namespace PatientBooking.Api.Domain;

public class ClinicOperatingHours
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    // Both null - closed all day. Both set - open/close window for this day.
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}
