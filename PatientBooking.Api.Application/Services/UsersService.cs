using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;
using System.Security.Claims;
using System.Text;

namespace PatientBooking.Api.Application.Services;

public class UsersService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<UsersService> logger,
    IHttpContextAccessor httpContextAccessor,
    PatientBookingDbContext patientBookingDbContext,
    IOptions<JwtSettings> jwtOptions,
    TimeProvider clock
) : IUsersService
{
    private const string _invalidCredentials = "Invalid Credentials";

    public async Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto)
    {
        ApplicationUser user = new()
        {
            Email = registerUserDto.Email,
            FirstName = registerUserDto.FirstName,
            LastName = registerUserDto.LastName,
            UserName = registerUserDto.Email,
        };

        // UserManager from Identity will handle the validations for uniqueness
        IdentityResult createResult = await userManager.CreateAsync(user, registerUserDto.Password);
        if (!createResult.Succeeded)
        {
            var registrationErrors = createResult.Errors
                    .Select(e => new ResultError(nameof(ErrorCodes.BadRequest), e.Description))
                    .ToArray();

            return Result<RegisteredUserDto>.BadRequest(registrationErrors);
        }

        // User now has an ID at this point, which we can use to finalise the creation
        RegisteredUserDto registeredUserDto = new()
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Id = user.Id,
        };

        return Result<RegisteredUserDto>.Success(registeredUserDto);
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct)
    {
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var user = await userManager.FindByEmailAsync(loginUserDto.Email);
        if (user is null)
        {
            return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Unauthorized), _invalidCredentials));
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user, loginUserDto.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return FailLockedOut(loginUserDto.Email, ipAddress);
        }

        if (signInResult.IsNotAllowed)
        {
            return FailEmailNotConfirmed(loginUserDto.Email, ipAddress);
        }

        if (!signInResult.Succeeded)
        {
            return FailWrongPassword(loginUserDto.Email, ipAddress);
        }

        var token = await GenerateTokenAsync(user, ct);
        return Result<LoginResponseDto>.Success(new LoginResponseDto { Token = token });
    }

    private async Task<string> GenerateTokenAsync(ApplicationUser user, CancellationToken ct)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
        ];

        // Single claim, not a list - roles are mutually exclusive by construction
        // (One profile table row per user), so there's no Select/Union over a role list
        // The way the IdentityRole version above needs
        var role = await ResolveRoleAsync(user.Id, ct);
        claims.Add(new Claim(ClaimTypes.Role, role));

        // Same key material Program.cs' JwtBearer setup validates against -
        // a mismatch here signs tokens that authenticate nowhere
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            Expires = clock.GetUtcNow().UtcDateTime.AddMinutes(jwtOptions.Value.DurationInMinutes),
            SigningCredentials = credentials,
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }

    private async Task<string> ResolveRoleAsync(string userId, CancellationToken ct)
    {
        if (await patientBookingDbContext.Admins.AnyAsync(a => a.UserId == userId, ct))
            return "Admin";
        if (await patientBookingDbContext.Employees.AnyAsync(e => e.UserId == userId, ct))
            return "Employee";
        if (await patientBookingDbContext.Patients.AnyAsync(p => p.UserId == userId, ct))
            return "Patient";

        throw new InvalidOperationException($"User {userId} has no associated role profile.");
    }

    private Result<LoginResponseDto> FailLockedOut(string email, string ipAddress)
    {
        logger.LoginBlockedLockedOut(email, ipAddress);
        return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidCredentials));
    }

    private Result<LoginResponseDto> FailEmailNotConfirmed(string email, string ipAddress)
    {
        logger.LoginBlockedEmailNotConfirmed(email, ipAddress);
        return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidCredentials));
    }

    private Result<LoginResponseDto> FailWrongPassword(string email, string ipAddress)
    {
        logger.LoginFailedWrongPassword(email, ipAddress);
        return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidCredentials));
    }
}
