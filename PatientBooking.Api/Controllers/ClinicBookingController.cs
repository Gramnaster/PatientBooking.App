using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Booking;
using PatientBooking.Api.AuthorizationFilters;

namespace PatientBooking.Api.Controllers;

// Employee access has their own ClinicId by AdminOrEmployeeAttribute
// Admins reach every clinic. Separate from BookingController, which is
// for patients scoped to caller's own bookings
[Route("api/clinic/{clinicId:int}/booking")]
[ApiController]
[Authorize]
[AdminOrEmployee]
public class ClinicBookingController(IBookingService bookingService) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetBookingDto>>> ListBookingsByClinicAsync(
        int clinicId,
        CancellationToken ct
    )
    {
        var result = await bookingService.ListBookingsByClinicAsync(clinicId, ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GetBookingDto>> GetBookingAsync(int clinicId, int id, CancellationToken ct)
    {
        var result = await bookingService.GetByIdForClinicAsync(clinicId, id, ct);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<GetBookingDto>> CreateBookingAsync(
        int clinicId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CreateBookingForPatientDto dto,
        CancellationToken ct
    )
    {
        var result = await bookingService.CreateForClinicAsync(clinicId, dto, idempotencyKey, ct);
        return ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelBookingAsync(int clinicId, int id, CancellationToken ct)
    {
        var result = await bookingService.CancelForClinicAsync(clinicId, id, ct);
        return ToActionResult(result);
    }
}
