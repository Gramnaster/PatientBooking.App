using PatientBooking.Api.Common.Enums;

namespace PatientBooking.Api.Domain;

public class BookingLineItem
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Price { get; set; }
    public LineItemType LineItemType { get; set; }

    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
