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
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetBookingDto>>> GetMyBookings(CancellationToken ct)
    {
        var result = await bookingService.GetMyBookingsAsync(ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GetBookingDto>> GetBookingAsync(int id, CancellationToken ct)
    {
        var result = await bookingService.GetByIdAsync(id, ct);
        return ToActionResult(result);
    }

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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelBookingAsync(int id, CancellationToken ct)
    {
        var result = await bookingService.CancelBookingAsync(id, ct);
        return ToActionResult(result);
    }
}
