using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto)
    {
        var result = await usersService.RegisterAsync(registerUserDto);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<string>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct)
    {
        var result = await usersService.LoginAsync(loginUserDto, ct);
        return ToActionResult(result);
    }

}
