using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Booking;

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Patient")]
public class BookingController(IBookingService bookingService) : BaseApiController
{
    /// <summary>Lists the caller's own bookings, most recent appointment first.</summary>
    /// <returns>
    /// Each entry's <c>id</c> is the booking's own identifier - use it, not <c>bookingNumber</c> or
    /// <c>patientId</c>, with <see cref="GetBookingAsync"/> and <see cref="CancelBookingAsync"/>.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetBookingDto>>> GetMyBookings(CancellationToken ct)
    {
        var result = await bookingService.GetMyBookingsAsync(ct);
        return ToActionResult(result);
    }

    /// <summary>Gets one of the caller's own bookings.</summary>
    /// <param name="id">The booking's own identifier, as returned in <c>id</c> by this controller's other endpoints.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<GetBookingDto>> GetBookingAsync(int id, CancellationToken ct)
    {
        var result = await bookingService.GetByIdAsync(id, ct);
        return ToActionResult(result);
    }

    /// <summary>Books an appointment for the caller.</summary>
    /// <returns>The created booking, including the <c>id</c> to use for cancellation.</returns>
    [HttpPost]
    public async Task<ActionResult<GetBookingDto>> CreateBookingAsync(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CreateBookingDto dto,
        CancellationToken ct
    )
    {
        var result = await bookingService.CreateBookingAsync(dto, idempotencyKey, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Cancels one of the caller's own bookings, freeing the appointment slot for others to book.
    /// </summary>
    /// <param name="id">
    /// The booking's own identifier (from <c>id</c> in a list/detail/creation response) - not the
    /// <c>bookingNumber</c> label.
    /// </param>
    /// <response code="204">Cancelled.</response>
    /// <response code="404">No such booking, it belongs to another patient, or it was already cancelled.</response>
    /// <response code="409">Less than 6 hours remain before the appointment - too late to cancel.</response>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelBookingAsync(int id, CancellationToken ct)
    {
        var result = await bookingService.CancelBookingAsync(id, ct);
        return ToActionResult(result);
    }
}
