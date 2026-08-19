using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Common.Results;

namespace PatientBooking.Api.Application.Contracts;

public interface IUsersService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct);
    Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto);
}