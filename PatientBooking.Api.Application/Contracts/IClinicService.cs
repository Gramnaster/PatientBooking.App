using System;
using System.Collections.Generic;
using System.Text;
using PatientBooking.Api.Application.DTOs.Clinic;
using PatientBooking.Api.Common.Results;

namespace PatientBooking.Api.Application.Contracts;

public interface IClinicService
{
    Task<Result<IReadOnlyList<GetClinicDto>>> GetClinicsAsync(CancellationToken ct);
    Task<Result<GetClinicDto>> GetClinicAsync(int clinicId, CancellationToken ct);
    Task<Result<GetClinicDto>> CreateClinicAsync(CreateClinicDto createDto, CancellationToken ct);
    Task<Result> UpdateClinicAsync(int id, UpdateClinicDto updateDto, CancellationToken ct);
    Task<Result> DeleteClinicAsync(int id, CancellationToken ct);
    Task<Result<IReadOnlyList<GetClinicOperatingHoursDto>>> GetOperatingHoursAsync(int clinicId, CancellationToken ct);
    Task<Result> UpdateOperatingHoursAsync(
        int clinicId,
        IReadOnlyList<UpdateClinicOperatingHoursDto> days,
        CancellationToken ct
    );
}
