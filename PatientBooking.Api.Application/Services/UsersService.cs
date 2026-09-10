using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Common.Results;
using PatientBooking.Api.Domain;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PatientBooking.Api.Application.Services;

#pragma warning disable S107
public class UsersService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<UsersService> logger,
    IHttpContextAccessor httpContextAccessor,
    PatientBookingDbContext patientBookingDbContext,
    IOptions<JwtSettings> jwtOptions,
    IEmailSender<ApplicationUser> emailSender,
    ILoginNotificationSender loginNotificationSender,
    IOptions<GoogleAuthSettings> googleAuthOptions,
    TimeProvider clock
) : IUsersService
#pragma warning restore S107
{
    private const string _userNotFound = "User not found.";
    private const string _invalidCredentials = "Invalid Credentials.";
    private const string _invalidRefreshTokens = "Invalid or expired refresh tokens.";

    // Matches ASP.NET Core Identity's default DataProtectionTokenProviderOptions.TokenLifespan (1
    // day) - Program.cs does not override it. If that default is ever configured explicitly, this
    // must change with it so a queued email's expiry can't outlive (or undercut) the token itself.
    private static readonly TimeSpan ConfirmationLinkLifespan = TimeSpan.FromDays(1);

    // 2FA Properties
    private const int PendingTokenMinutes = 5;
    private const int RecoveryCodeCount = 10;
    private string PendingAudience => $"{jwtOptions.Value.Audience}:2fa-pending";

    public string UserId =>
        httpContextAccessor?.HttpContext?.User?.FindFirst(
            JwtRegisteredClaimNames.Sub
        )?.Value ?? httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    public async Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registerUserDto, CancellationToken ct)
    {
        ApplicationUser user = new()
        {
            Email = registerUserDto.Email,
            FirstName = registerUserDto.FirstName,
            LastName = registerUserDto.LastName,
            UserName = registerUserDto.Email,
            CreatedAtUtc = clock.GetUtcNow(),
        };

        await using var transaction = await patientBookingDbContext.Database.BeginTransactionAsync(ct);

        // UserManager from Identity will handle the validations for uniqueness
        IdentityResult createResult = await userManager.CreateAsync(user, registerUserDto.Password);
        if (!createResult.Succeeded)
        {
            var registrationErrors = createResult
                .Errors
                .Select(e => new ResultError(nameof(ErrorCodes.BadRequest), e.Description))
                .ToArray();

            return Result<RegisteredUserDto>.BadRequest(registrationErrors);
        }

        // MRN needs patient.Id, which doesn't exist until this save assigns it
        // so Patient row is saved once without it. Fail = user created with no Patient profile
        Patient patient = new() { UserId = user.Id, CreatedAtUtc = clock.GetUtcNow() };

        patientBookingDbContext.Patients.Add(patient);

        try
        {
            await patientBookingDbContext.SaveChangesAsync(ct);
            patient.MedicalRecordNumber = IdentifierCodeEncoder.Encode(
                patient.Id,
                IdentifierCodeEncoder.MedicalRecordNumberShape
            );

            // Queues the confirmation email in the same transaction instead of awaiting SMTP here -
            // a background worker delivers it, so broker/SMTP downtime can no longer fail registration.
            await EnqueueConfirmationEmailAsync(user, ct);

            await patientBookingDbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<RegisteredUserDto>.Conflict("Could not create a patient profile. Please try again.");
        }

        // User now has an ID at this point, which we can use to finalise the creation
        RegisteredUserDto registeredUserDto = new()
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Id = user.Id,
            MedicalRecordNumber = patient.MedicalRecordNumber,
        };

        return Result<RegisteredUserDto>.Success(registeredUserDto);
    }

    public async Task<Result> ConfirmEmailAsync(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.NotFound(_userNotFound);
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return Result.BadRequest(new ResultError(nameof(ErrorCodes.BadRequest), "Invalid confirmation token"));
        }

        IdentityResult confirmResult = await userManager.ConfirmEmailAsync(user, decodedToken);
        if (confirmResult.Succeeded)
        {
            return Result.Success();
        }

        var confirmationErrors = confirmResult
            .Errors
            .Select(error => new ResultError(nameof(ErrorCodes.BadRequest), error.Description))
            .ToArray();

        return Result.BadRequest(confirmationErrors);
    }

    public async Task<Result> ResendConfirmationEmailAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
        {
            await SendConfirmationEmailAsync(user);
        }

        return Result.Success();
    }

    private async Task<string> BuildConfirmationLinkAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var request = httpContextAccessor.HttpContext!.Request;
        return $"{request.Scheme}://{request.Host}/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(encodedToken)}";
    }

    // Used by ResendConfirmationEmailAsync only - unchanged synchronous SMTP send, out of this task's scope.
    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var confirmationLink = await BuildConfirmationLinkAsync(user);
        await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink);
    }

    // Registration path: queues the email in the outbox instead of sending it inline, so the caller
    // gets Result<RegisteredUserDto>.Success once the transaction below commits, not once SMTP replies.
    private async Task EnqueueConfirmationEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var confirmationLink = await BuildConfirmationLinkAsync(user);
        (string subject, string htmlBody) = ComposeConfirmationEmail(confirmationLink);
        DateTimeOffset now = clock.GetUtcNow();

        // Same subject/body SendConfirmationEmailAsync has always sent - only the composition point
        // moved, from SmtpIdentityEmailSender (post-dequeue) to here (pre-stage), so the shared
        // consumer never needs registration-specific formatting knowledge. Expiry is aligned to the
        // token's own lifespan: a link delivered after the token has expired would just fail
        // ConfirmEmailAsync anyway, so there is no point sending (or recording as sent) an email past
        // that point.
        EmailEnvelope evt = new(
            Guid.CreateVersion7(),
            user.Email!,
            subject,
            htmlBody,
            PlainTextBody: null,
            now,
            now + ConfirmationLinkLifespan,
            Kind: "RegistrationConfirmation"
        );

        EmailOutboxMessage outboxMessage = new()
        {
            Id = evt.NotificationId,
            Recipient = user.Email!,
            Kind = evt.Kind,
            Payload = JsonSerializer.Serialize(evt),
            CreatedAtUtc = now,
        };

        await patientBookingDbContext.AddAsync(outboxMessage, ct);
    }

    // Same subject/body as SendConfirmationEmailAsync above.
    private static (string Subject, string HtmlBody) ComposeConfirmationEmail(string confirmationLink) =>
        ("Confirm your email", $"""Please confirm your Patient Booking account by <a href="{WebUtility.HtmlEncode(confirmationLink)}">clicking here</a>""");

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto, CancellationToken ct)
    {
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var user = await userManager.FindByEmailAsync(loginUserDto.Email);
        if (user is null)
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.Unauthorized), _invalidCredentials)
            );
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            loginUserDto.Password,
            lockoutOnFailure: true
        );

        if (signInResult.IsLockedOut)
        {
            return FailLockedOut(loginUserDto.Email, ipAddress);
        }

        if (signInResult.IsNotAllowed)
        {
            bool hasCorrectPassword = await userManager.CheckPasswordAsync(user, loginUserDto.Password);

            if (hasCorrectPassword && !await userManager.IsEmailConfirmedAsync(user))
            {
                return FailEmailNotConfirmed(loginUserDto.Email, ipAddress);
            }

            return FailWrongPassword(loginUserDto.Email, ipAddress);
        }

        if (!signInResult.Succeeded)
        {
            return FailWrongPassword(loginUserDto.Email, ipAddress);
        }

        if (user.TwoFactorEnabled)
        {
            LoginResponseDto loginResponseDto = new()
            {
                RequiresTwoFactor = true,
                PendingToken = GeneratePendingToken(user),
            };
            return Result<LoginResponseDto>.Success(loginResponseDto);
        }

        var token = await GenerateTokenAsync(user, ct);
        return Result<LoginResponseDto>.Success(new LoginResponseDto { Token = token });
    }

    private Result<LoginResponseDto> FailLockedOut(string email, string ipAddress)
    {
        logger.LoginBlockedLockedOut(email, ipAddress);
        return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Unauthorized), _invalidCredentials));
    }

    private Result<LoginResponseDto> FailEmailNotConfirmed(string email, string ipAddress)
    {
        logger.LoginBlockedEmailNotConfirmed(email, ipAddress);
        return Result<LoginResponseDto>.Failure(
            new ResultError(
                nameof(ErrorCodes.Forbid),
                "Confirm your email before signing in. Request a new confirmation email if needed."
            )
        );
    }

    private Result<LoginResponseDto> FailWrongPassword(string email, string ipAddress)
    {
        logger.LoginFailedWrongPassword(email, ipAddress);
        return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Unauthorized), _invalidCredentials));
    }

    // No roles / email claims - this token proves "password already checked for this userId" and
    // nothing else, so VerifyTwoFactorLoginAsync only trusts its "sub" claim
    private string GeneratePendingToken(ApplicationUser user)
    {
        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwtOptions.Value.Issuer,
            Audience = PendingAudience,
            Expires = clock.GetUtcNow().UtcDateTime.AddMinutes(PendingTokenMinutes),
            SigningCredentials = credentials,
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }

    public async Task<Result> ForgotPasswordAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await SendPasswordResetEmailAsync(user);
        }

        return Result.Success();
    }

    private async Task SendPasswordResetEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var request = httpContextAccessor.HttpContext!.Request;
        var resetLink =
            $"{request.Scheme}://{request.Host}/api/auth/reset-password?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(encodedToken)}";

        await emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);
    }

    // Caller has to present a real UserId + Token and you already need an email to attempt this
    public async Task<Result> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var user = await userManager.FindByIdAsync(resetPasswordDto.UserId);
        if (user is null)
        {
            return Result.NotFound(_userNotFound);
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetPasswordDto.Token));
        }
        catch (FormatException)
        {
            return Result.Failure(new ResultError(nameof(ErrorCodes.BadRequest), "Invalid reset token"));
        }

        IdentityResult resetResult = await userManager.ResetPasswordAsync(
            user,
            decodedToken,
            resetPasswordDto.NewPassword
        );

        return resetResult.Succeeded ? Result.Success() : Result.Failure(ToResultError(resetResult.Errors));
    }

    private static ResultError[] ToResultError(IEnumerable<IdentityError> errors) =>
        errors.Select(e => new ResultError(nameof(ErrorCodes.BadRequest), e.Description)).ToArray();

    public async Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var tokenHash = HashToken(refreshToken);
        var existing = await patientBookingDbContext
            .RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existing is null || existing.User is null)
        {
            return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidRefreshTokens));
        }

        if (existing.RevokedAtUtc is not null)
        {
            logger.RefreshTokenReuseDetected(existing.UserId);
            await RevokeAllActiveTokensAsync(existing.UserId, ct);
            return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidRefreshTokens));
        }

        if (!existing.IsActive || existing.User.DeletedAtUtc is not null)
        {
            return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidRefreshTokens));
        }

        var rawReplacement = GenerateRawToken();
        var replacement = BuildRefreshToken(existing.UserId, rawReplacement);

        existing.RevokedAtUtc = clock.GetUtcNow();
        existing.ReplacedByTokenHash = replacement.TokenHash;
        patientBookingDbContext.RefreshTokens.Add(replacement);

        var accessToken = await GenerateTokenAsync(existing.User, ct);

        try
        {
            await patientBookingDbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // After exception, `existing` is still tracked as `Modified` with its original `RowVersion` snapshot
            patientBookingDbContext.ChangeTracker.Clear();
            await RevokeAllActiveTokensAsync(existing.UserId, ct);
            return Result<LoginResponseDto>.Failure(new ResultError(nameof(ErrorCodes.Forbid), _invalidRefreshTokens));
        }

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Token = accessToken,
            RefreshToken = rawReplacement,
        });
    }

    private static string GenerateRawToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task RevokeAllActiveTokensAsync(string userId, CancellationToken ct)
    {
        var activeTokens = await patientBookingDbContext
            .RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        await patientBookingDbContext.SaveChangesAsync(ct);
    }
    private RefreshToken BuildRefreshToken(string userId, string rawToken) =>
        new()
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = clock.GetUtcNow().AddDays(jwtOptions.Value.RefreshTokenDurationInDays),
        };

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
        if (await patientBookingDbContext.Employees.AnyAsync(e => e.UserId == userId && e.DeletedAtUtc == null, ct))
            return "Employee";
        if (await patientBookingDbContext.Patients.AnyAsync(p => p.UserId == userId, ct))
            return "Patient";

        throw new InvalidOperationException($"User {userId} has no associated role profile.");
    }

    // Idempotent. Revoking an unknown or already-revoked token still reorts Success, so a client
    // retrying logout (e.g., after a dropped response) doesn't get an error for something true.
    public async Task<Result> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var tokenHash = HashToken(refreshToken);
        var existing = await patientBookingDbContext.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash,
            ct
        );

        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.RevokedAtUtc = clock.GetUtcNow();
            await patientBookingDbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    // Metadata only (Id, CreatedAt, ExpiresAt) - never the token or hash, same never returning a password hash.
    // Scoped to userId (the caller's own "sub" claim, not a route parameter), so no way to list or target...
    // another user's sessions.
    public async Task<Result<IEnumerable<RefreshTokenSessionDto>>> GetActiveSessionsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var sessions = await patientBookingDbContext
            .RefreshTokens
            .Where(t => t.UserId == UserId && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(
                t => new RefreshTokenSessionDto
                {
                    Id = t.Id,
                    CreatedAtUtc = t.CreatedAtUtc,
                    ExpiresAtUtc = t.ExpiresAtUtc,
                }
            )
            .ToListAsync(ct);

        return Result<IEnumerable<RefreshTokenSessionDto>>.Success(sessions);
    }

    // Look up excludes other user's rows, so a sessionId belonging to someone else 404s here
    // rather than reaching an ownership check
    public async Task<Result> RevokeSessionsAsync(int sessionId, CancellationToken ct)
    {
        var session = await patientBookingDbContext.RefreshTokens.FirstOrDefaultAsync(
            t => t.Id == sessionId && t.UserId == UserId,
            ct
        );

        if (session is null)
        {
            return Result.NotFound("Session not found");
        }

        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = clock.GetUtcNow();
            await patientBookingDbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    // Creates the Authenticator URI for compatibility with existing products
    public async Task<Result<TwoFactorSetupDto>> GetTwoFactorSetupAsync()
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null)
        {
            return Result<TwoFactorSetupDto>.NotFound(_userNotFound);
        }

        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        TwoFactorSetupDto twoFactorSetupDto = new()
        {
            SharedKey = unformattedKey!,
            AuthenticatorUri = BuildAuthenticatorUri(user, unformattedKey!, jwtOptions.Value.Issuer),
        };
        return Result<TwoFactorSetupDto>.Success(twoFactorSetupDto);
    }

    // Mirrors Identity UI's own EnableAuthenticator page format, which is what every authenticator app
    // (Google, Authy, etc.) expects to scan as a QR code.
    private static string BuildAuthenticatorUri(ApplicationUser user, string unformattedKey, string issuer) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email!)}" +
            $"?secret={unformattedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

    public async Task<Result<TwoFactorEnabledDto>> EnableTwoFactorAsync(TwoFactorCodeDto codeDto)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null)
        {
            return Result<TwoFactorEnabledDto>.NotFound(_userNotFound);
        }

        var isCodeValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            codeDto.Code
        );
        if (!isCodeValid)
        {
            return Result<TwoFactorEnabledDto>.Failure(
                new ResultError(nameof(ErrorCodes.BadRequest), "Invalid authenticator code")
            );
        }

        await userManager.SetTwoFactorEnabledAsync(user, enabled: true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        TwoFactorEnabledDto twoFactorEnabledDto = new() { RecoveryCodes = recoveryCodes ?? [], };

        return Result<TwoFactorEnabledDto>.Success(twoFactorEnabledDto);
    }

    // Resets auth key too - re-enabling later requires a fresh QR scan
    // Matches Identity UI's own disable behaviour, rather than silently letting stale key work again
    public async Task<Result> DisableTwoFactorAsync()
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null)
        {
            return Result.NotFound(_userNotFound);
        }

        await userManager.SetTwoFactorEnabledAsync(user, enabled: false);
        await userManager.ResetAuthenticatorKeyAsync(user);

        return Result.Success();
    }

    public async Task<Result<LoginResponseDto>> VerifyTwoFactorLoginAsync(
        string pendingToken,
        string code,
        CancellationToken ct
    )
    {
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        string userId;
        try
        {
            // Try to validate pending token's signature/issuer/audience/lifetime the same way JwtBearer would
            // Forged or expired token fails here before ever touching a user record
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Value.Issuer,
                ValidAudience = PendingAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key)),
                ClockSkew = TimeSpan.Zero,
            };

            TokenValidationResult validationResult = await new JsonWebTokenHandler().ValidateTokenAsync(
                pendingToken,
                validationParameters
            );
            if (!validationResult.IsValid)
            {
                throw validationResult.Exception ?? new SecurityTokenException("Pending token failed validation.");
            }

            userId = validationResult.ClaimsIdentity.FindFirst(
                JwtRegisteredClaimNames.Sub
            )?.Value ?? validationResult.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                throw new SecurityTokenException("Pending token is missing a subject claim.");
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(
                    nameof(ErrorCodes.Unauthorized),
                    "Invalid or expired two-factor session. Please login again."
                )
            );
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.Unauthorized), "Invalid two-factor session.")
            );
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(
                    nameof(ErrorCodes.Forbid),
                    "Account temporarily locked due to repeated failed attempts. Please try again later."
                )
            );
        }

        var isTotpValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            code
        );
        var isValid = isTotpValid ||
            await userManager.RedeemTwoFactorRecoveryCodeAsync(user, code) is { Succeeded: true };
        if (!isValid)
        {
            logger.InvalidTwoFactorCode(userId);
            await userManager.AccessFailedAsync(user);
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.Unauthorized), "invalid authentication code")
            );
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return Result<LoginResponseDto>.Success(await IssueTokenPairAsync(user, ipAddress, ct));
    }

    private async Task<LoginResponseDto> IssueTokenPairAsync(
        ApplicationUser user,
        string ipAddress,
        CancellationToken ct
    )
    {
        var accessToken = await GenerateTokenAsync(user, ct);
        var rawRefreshToken = GenerateRawToken();

        patientBookingDbContext.RefreshTokens.Add(BuildRefreshToken(user.Id, rawRefreshToken));
        await patientBookingDbContext.SaveChangesAsync(ct);

        await loginNotificationSender.SendLoginNotificationAsync(user, ipAddress, clock.GetUtcNow(), ct);

        return new LoginResponseDto { Token = accessToken, RefreshToken = rawRefreshToken };
    }

    public async Task<Result<LoginResponseDto>> ExternalLoginAsync(
        ExternalLoginDto externalLoginDto,
        CancellationToken ct
    )
    {
        if (!externalLoginDto.Provider.Equals("Google", StringComparison.Ordinal))
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.BadRequest), "Unsupported external provider")
            );
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            GoogleJsonWebSignature.ValidationSettings validationSettings = new()
            {
                Audience = [googleAuthOptions.Value.ClientId],
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(externalLoginDto.IdToken, validationSettings);
        }
        catch (InvalidJwtException)
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.Unauthorized), "Invalid or expired external login token.")
            );
        }

        if (!payload.EmailVerified)
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.Forbid), "Google acount email is not verified.")
            );
        }

        var userResult = await FindOrCreateGoogleUserAsync(payload, ct);
        if (!userResult.IsSuccess)
        {
            return Result<LoginResponseDto>.Failure(userResult.Errors);
        }

        var user = userResult.Value!;

        // External login bypasses SignInManager's lockout check entirely
        // Soft-delete needs its own explicit guard here - the only path in this file
        // that does, since LoginAsync gets this for free via IsLockedOut
        if (user.DeletedAtUtc is not null)
        {
            return Result<LoginResponseDto>.Failure(
                new ResultError(nameof(ErrorCodes.Forbid), "This account has been deactivated.")
            );
        }

        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        return Result<LoginResponseDto>.Success(await IssueTokenPairAsync(user, ipAddress, ct));
    }

    private async Task<Result<ApplicationUser>> FindOrCreateGoogleUserAsync(
        GoogleJsonWebSignature.Payload payload,
        CancellationToken ct
    )
    {
        var user = await userManager.FindByLoginAsync("Google", payload.Subject);
        if (user is not null)
        {
            return Result<ApplicationUser>.Success(user);
        }

        user = await userManager.FindByEmailAsync(payload.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = payload.Email,
                UserName = payload.Email,
                FirstName = payload.GivenName ?? string.Empty,
                LastName = payload.FamilyName ?? string.Empty,
                EmailConfirmed = true,
                CreatedAtUtc = clock.GetUtcNow(),
            };

            IdentityResult createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return Result<ApplicationUser>.Failure(ToResultError(createResult.Errors));
            }

            patientBookingDbContext.Patients.Add(new Patient { UserId = user.Id, CreatedAtUtc = clock.GetUtcNow() });
            await patientBookingDbContext.SaveChangesAsync(ct);
        }

        var linkResult = await userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
        return linkResult.Succeeded
            ? Result<ApplicationUser>.Success(user)
            : Result<ApplicationUser>.Failure(ToResultError(linkResult.Errors));
    }

    public async Task<Result> SoftDeleteAccountAsync(string? password, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null)
            return Result.NotFound(_userNotFound);

        var passwordError = await ConfirmPasswordIfRequiredAsync(user, password);
        if (passwordError is not null)
            return passwordError.Value;

        var lastAdminError = await BlockIfLastAdminAsync(user);
        return lastAdminError ?? await SoftDeleteCoreAsync(user, ct);
    }

    private async Task<Result> SoftDeleteCoreAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.DeletedAtUtc is not null)
            return Result.Success();

        user.UpdatedAtUtc = clock.GetUtcNow();
        user.DeletedAtUtc = clock.GetUtcNow();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        IdentityResult updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Failure(ToResultError(updateResult.Errors));
        }

        await userManager.UpdateSecurityStampAsync(user);
        await RevokeAllActiveTokensAsync(user.Id, ct);

        return Result.Success();
    }

    public async Task<Result> HardDeleteAccountAsync(string? password, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null)
        {
            return Result.NotFound(_userNotFound);
        }

        var passwordError = await ConfirmPasswordIfRequiredAsync(user, password);
        if (passwordError is not null)
        {
            return passwordError.Value;
        }

        var lastAdminError = await BlockIfLastAdminAsync(user);
        return lastAdminError ?? await HardDeleteCoreAsync(user);
    }

    // Admin path, by target userId - no password re-auth here (an admin can't know a target's
    // password). Role membership, checked by [Authorize(Roles = "Admin")] at the controller, is
    // the gate instead. Success is logged as a security-relevant audit event.
    public async Task<Result> AdminSoftDeleteUserAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.NotFound(_userNotFound);
        }

        var lastAdminError = await BlockIfLastAdminAsync(user);
        if (lastAdminError is not null)
        {
            return lastAdminError.Value;
        }

        var result = await SoftDeleteCoreAsync(user, ct);
        if (result.IsSuccess)
        {
            logger.AccountSoftDeletedByAdmin(UserId, userId);
        }

        return result;
    }

    public async Task<Result> AdminHardDeleteUserAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.NotFound(_userNotFound);
        }

        var lastAdminError = await BlockIfLastAdminAsync(user);
        if (lastAdminError is not null)
        {
            return lastAdminError.Value;
        }

        var result = await HardDeleteCoreAsync(user);
        if (result.IsSuccess)
        {
            logger.AccountHardDeletedByAdmin(UserId, userId);
        }

        return result;
    }

    private async Task<Result> HardDeleteCoreAsync(ApplicationUser user)
    {
        IdentityResult deleteResult = await userManager.DeleteAsync(user);
        return deleteResult.Succeeded ? Result.Success() : Result.Failure(ToResultError(deleteResult.Errors));
    }

    private async Task<Result?> ConfirmPasswordIfRequiredAsync(ApplicationUser user, string? password)
    {
        if (!await userManager.HasPasswordAsync(user))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure(new ResultError(nameof(ErrorCodes.BadRequest), "Password confirmation is required."));
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        return passwordValid
            ? null
            : Result.Failure(new ResultError(nameof(ErrorCodes.Forbid), "Password is incorrect."));
    }

    private async Task<Result?> BlockIfLastAdminAsync(ApplicationUser user)
    {
        bool isAdmin = await patientBookingDbContext.Admins.AnyAsync(a => a.UserId == user.Id);
        if (!isAdmin)
        {
            return null;
        }

        var anotherAdminExists = await patientBookingDbContext.Admins.AnyAsync(a => a.UserId != user.Id);
        return anotherAdminExists ? null : Result.Conflict("Cannot remove the last remaining admin account.");
    }
}
