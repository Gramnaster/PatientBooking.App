using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Auth;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IUsersService usersService) : BaseApiController
{
    // WIP: Needs to register the IUsersService
    // POST: api/<AuthController>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto, CancellationToken ct)
    {
        var result = await usersService.RegisterAsync(registerUserDto, ct);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct)
    {
        var result = await usersService.LoginAsync(loginUserDto, ct);
        return ToActionResult(result);
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<ActionResult> ConfirmEmailAsync([FromQuery] string userId, [FromQuery] string token)
    {
        var result = await usersService.ConfirmEmailAsync(userId, token);
        return ToActionResult(result);
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<ActionResult> ResendConfirmationAsync(ResendConfirmationDto resendConfirmationDto)
    {
        var result = await usersService.ResendConfirmationEmailAsync(resendConfirmationDto.Email);
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        var result = await usersService.ForgotPasswordAsync(forgotPasswordDto.Email);
        return ToActionResult(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var result = await usersService.ResetPasswordAsync(resetPasswordDto);
        return ToActionResult(result);
    }

    // Anonymous. Caller's access token may already be expired (which is the point of calling this),
    // So only the refresh token itself is checked.
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto requestDto, CancellationToken ct)
    {
        var result = await usersService.RefreshTokenAsync(requestDto.RefreshToken, ct);
        return ToActionResult(result);
    }

    // Requires the caller's own bearer token, unlike refresh - logout is a deliberate action taken by 
    // an authenticated session, not a credential-recovery path
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> LogoutAsync(RefreshTokenRequestDto requestDto, CancellationToken ct)
    {
        var result = await usersService.RevokeRefreshTokenAsync(requestDto.RefreshToken, ct);
        return ToActionResult(result);
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<RefreshTokenSessionDto>>> GetActiveSessionsAsync(CancellationToken ct)
    {
        var result = await usersService.GetActiveSessionsAsync(ct);
        return ToActionResult(result);
    }

    [HttpDelete("sessions/{sessionId:int}")]
    [Authorize]
    public async Task<ActionResult> RevokeSessionsAsync(int sessionid, CancellationToken ct)
    {
        var result = await usersService.RevokeSessionsAsync(sessionid, ct);
        return ToActionResult(result);
    }

    [HttpGet("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<TwoFactorSetupDto>> GetTwoFactorSetupAsync()
    {
        var result = await usersService.GetTwoFactorSetupAsync();
        return ToActionResult(result);
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<ActionResult<TwoFactorEnabledDto>> EnableTwoFactorAsync(TwoFactorCodeDto codeDto)
    {
        var result = await usersService.EnableTwoFactorAsync(codeDto);
        return ToActionResult(result);
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<ActionResult> DisableTwoFactorAsync()
    {
        var result = await usersService.DisableTwoFactorAsync();
        return ToActionResult(result);
    }

    [HttpPost("2fa/login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> VerifyTwoFactorLoginAsync(
        [FromHeader(Name = "Authorization")] string? authorization,
        TwoFactorCodeDto codeDto,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var pendingToken = authorization["Bearer ".Length..].Trim();

        var result = await usersService.VerifyTwoFactorLoginAsync(pendingToken, codeDto.Code, ct);
        return ToActionResult(result);
    }

    [HttpPost("external")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> ExternalLoginAsync(ExternalLoginDto externalLoginDto, CancellationToken ct)
    {
        var result = await usersService.ExternalLoginAsync(externalLoginDto, ct);
        return ToActionResult(result);
    }

    [HttpDelete("account")]
    [Authorize]
    public async Task<ActionResult> SoftDeleteAccountAsync(DeleteAccountDto deleteAccountDto, CancellationToken ct)
    {
        var result = await usersService.SoftDeleteAccountAsync(deleteAccountDto.Password, ct);
        return ToActionResult(result);
    }

    [HttpDelete("account/permanent")]
    [Authorize]
    public async Task<ActionResult> HardDeleteAccountAsync(DeleteAccountDto deleteAccount, CancellationToken ct)
    {
        var result = await usersService.HardDeleteAccountAsync(deleteAccount.Password, ct);
        return ToActionResult(result);
    }

    [HttpDelete("users/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AdminSoftDeleteUserAsync(string userId, CancellationToken ct)
    {
        var result = await usersService.AdminSoftDeleteUserAsync(userId, ct);
        return ToActionResult(result);
    }

    [HttpDelete("users/{userId}/permanent")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AdminHardDeleteUserAsync(string userId, CancellationToken ct)
    {
        var result = await usersService.AdminHardDeleteUserAsync(userId, ct);
        return ToActionResult(result);
    }
}
