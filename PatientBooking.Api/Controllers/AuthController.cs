using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Auth;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public class AuthController(IUsersService usersService) : BaseApiController
{
    // WIP: Needs to register the IUsersService
    // POST: api/<AuthController>
    [HttpPost("register")]
    public async Task<ActionResult<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto, CancellationToken ct)
    {
        var result = await usersService.RegisterAsync(registerUserDto, ct);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct)
    {
        var result = await usersService.LoginAsync(loginUserDto, ct);
        return ToActionResult(result);
    }

    [HttpGet("confirm-email")]
    public async Task<ActionResult> ConfirmEmailAsync([FromQuery] string userId, [FromQuery] string token)
    {
        var result = await usersService.ConfirmEmailAsync(userId, token);
        return ToActionResult(result);
    }

    [HttpPost("resend-confirmation")]
    public async Task<ActionResult> ResendConfirmationAsync(ResendConfirmationDto resendConfirmationDto)
    {
        var result = await usersService.ResendConfirmationEmailAsync(resendConfirmationDto.Email);
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        var result = await usersService.ForgotPasswordAsync(forgotPasswordDto.Email);
        return ToActionResult(result);
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var result = await usersService.ResetPasswordAsync(resetPasswordDto);
        return ToActionResult(result);
    }
}
