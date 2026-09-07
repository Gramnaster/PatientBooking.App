using PatientBooking.Api.Application.DTOs.Clinic;
using PatientBooking.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace PatientBooking.Api.Application.Mappers;

[Mapper]
internal static partial class ClinicMapper
{
    public static partial IQueryable<GetClinicDto> ProjectToGetClinicDto(this IQueryable<Clinic> query);

    [MapperIgnoreSource(nameof(Clinic.EmployeeCount))]
    [MapperIgnoreSource(nameof(Clinic.Employees))]
    [MapperIgnoreSource(nameof(Clinic.Patients))]
    [MapperIgnoreSource(nameof(Clinic.OperatingHours))]
    [MapperIgnoreSource(nameof(Clinic.CreatedAtUtc))]
    [MapperIgnoreSource(nameof(Clinic.UpdatedAtUtc))]
    [MapperIgnoreSource(nameof(Clinic.DeletedAtUtc))]
    public static partial GetClinicDto ToGetClinicDto(Clinic clinic);

    [MapperIgnoreTarget(nameof(Clinic.Id))]
    [MapperIgnoreTarget(nameof(Clinic.Patients))]
    [MapperIgnoreTarget(nameof(Clinic.Employees))]
    [MapperIgnoreTarget(nameof(Clinic.EmployeeCount))]
    [MapperIgnoreTarget(nameof(Clinic.OperatingHours))]
    [MapperIgnoreTarget(nameof(Clinic.CreatedAtUtc))]
    [MapperIgnoreTarget(nameof(Clinic.UpdatedAtUtc))]
    [MapperIgnoreTarget(nameof(Clinic.DeletedAtUtc))]
    public static partial Clinic ToClinic(CreateClinicDto dto);

    [MapperIgnoreTarget(nameof(Clinic.Id))]
    [MapperIgnoreTarget(nameof(Clinic.Patients))]
    [MapperIgnoreTarget(nameof(Clinic.Employees))]
    [MapperIgnoreTarget(nameof(Clinic.EmployeeCount))]
    [MapperIgnoreTarget(nameof(Clinic.OperatingHours))]
    [MapperIgnoreTarget(nameof(Clinic.CreatedAtUtc))]
    [MapperIgnoreTarget(nameof(Clinic.UpdatedAtUtc))]
    [MapperIgnoreTarget(nameof(Clinic.DeletedAtUtc))]
    [MapperIgnoreSource(nameof(UpdateClinicDto.Id))]
    public static partial void UpdateClinic(UpdateClinicDto dto, Clinic clinic);
}
