using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Contracts;

public interface IUsersService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct);
    Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto, CancellationToken ct);
    Task<Result> ConfirmEmailAsync(string userId, string token);
    Task<Result> ResendConfirmationEmailAsync(string email);
    Task<Result> ForgotPasswordAsync(string email);
    Task<Result> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
    Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task<Result> RevokeSessionsAsync(int sessionId, CancellationToken ct);
    Task<Result> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task<Result<IEnumerable<RefreshTokenSessionDto>>> GetActiveSessionsAsync(CancellationToken ct);
    Task<Result<TwoFactorSetupDto>> GetTwoFactorSetupAsync();
    Task<Result<TwoFactorEnabledDto>> EnableTwoFactorAsync(TwoFactorCodeDto codeDto);
    Task<Result> DisableTwoFactorAsync();
    Task<Result<LoginResponseDto>> VerifyTwoFactorLoginAsync(string pendingToken, string code, CancellationToken ct);
    Task<Result<LoginResponseDto>> ExternalLoginAsync(ExternalLoginDto externalLoginDto, CancellationToken ct);
    Task<Result> SoftDeleteAccountAsync(string? password, CancellationToken ct);
    Task<Result> HardDeleteAccountAsync(string? password, CancellationToken ct);
    Task<Result> AdminSoftDeleteUserAsync(string userId, CancellationToken ct);
    Task<Result> AdminHardDeleteUserAsync(string userId, CancellationToken ct);
}
