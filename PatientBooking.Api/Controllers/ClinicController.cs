using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Clinic;

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClinicController(IClinicService clinicService) : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GetClinicDto>>> GetClinicsAsync(CancellationToken ct)
    {
        var result = await clinicService.GetClinicsAsync(ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<GetClinicDto>> GetClinicAsync(int id, CancellationToken ct)
    {
        var result = await clinicService.GetClinicAsync(id, ct);
        return ToActionResult<GetClinicDto>(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GetClinicDto>> CreateClinicAsync(CreateClinicDto createDto, CancellationToken ct)
    {
        var result = await clinicService.CreateClinicAsync(createDto, ct);
        return ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateClinicAsync(int id, UpdateClinicDto updateDto, CancellationToken ct)
    {
        var result = await clinicService.UpdateClinicAsync(id, updateDto, ct);
        return ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClinicAsync(int id, CancellationToken ct)
    {
        var result = await clinicService.DeleteClinicAsync(id, ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}/operating-hours")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GetClinicOperatingHoursDto>>> GetOperatingHoursAsync(
        int id,
        CancellationToken ct
    )
    {
        var result = await clinicService.GetOperatingHoursAsync(id, ct);
        return ToActionResult(result);
    }

    [HttpPut("{id:int}/operating-hours")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateOperatingHoursAsync(
        int id,
        IReadOnlyList<UpdateClinicOperatingHoursDto> days,
        CancellationToken ct
    )
    {
        var result = await clinicService.UpdateOperatingHoursAsync(id, days, ct);
        return ToActionResult(result);
    }
}
