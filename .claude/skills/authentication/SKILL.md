---
name: authentication
description: >
  Authentication and authorization for ASP.NET Core. Covers JWT bearer tokens,
  OpenID Connect, ASP.NET Identity, authorization policies, role and claim-based
  authorization, and API key authentication.
  Load this skill when implementing login, protecting endpoints, designing
  authorization rules, or when the user mentions "auth", "JWT", "bearer token",
  "OIDC", "OpenID Connect", "Identity", "claims", "roles", "authorize",
  "RequireAuthorization", "API key", or "cookie auth".
---

# Authentication & Authorization

## Core Principles

1. **Use ASP.NET Identity for user management** — Don't build your own user store. Identity handles password hashing, lockout, two-factor, and email confirmation.
2. **JWT for APIs, cookies for web apps** — APIs use Bearer token authentication; Blazor/MVC apps use cookie authentication.
3. **Policy-based authorization over roles** — Policies are testable, composable, and more expressive than `[Authorize(Roles = "Admin")]`.
4. **Never store secrets in code** — Use user secrets in development, Azure Key Vault / environment variables in production.

## Patterns

### JWT Bearer Authentication

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
```

### Token Generation

```csharp
public class TokenService(IConfiguration config, TimeProvider clock)
{
    public string GenerateToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.UserName!)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: clock.GetUtcNow().AddHours(1).DateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### Policy-Based Authorization

```csharp
// Define policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("CanManageOrders", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "orders:write"))
    .AddPolicy("MinimumAge", policy => policy
        .AddRequirements(new MinimumAgeRequirement(18)));

// Custom requirement + handler
public class MinimumAgeRequirement(int minimumAge) : IAuthorizationRequirement
{
    public int MinimumAge => minimumAge;
}

public class MinimumAgeHandler(TimeProvider clock) : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var dateOfBirthClaim = context.User.FindFirst("date_of_birth");
        if (dateOfBirthClaim is not null &&
            DateOnly.TryParse(dateOfBirthClaim.Value, out var dob) &&
            dob.AddYears(requirement.MinimumAge) <= DateOnly.FromDateTime(clock.GetUtcNow().DateTime))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

### Resource-Scoped Authorization (Custom Filters)

Policy-based authorization answers "does this user have permission X?" A resource-scoped check
answers a different question — "does this user have permission X on *this specific* resource?" —
where the resource id comes off the route. `AuthorizationHandler<T>` has no built-in access to
route data; a custom MVC filter does.

Two variants, chosen by whether membership can change without the user re-authenticating:

**Membership can change at runtime** (many-to-many, admin-assignable, no re-login expected) —
needs a per-request DB check. The attribute can't take a constructor-injected `DbContext`
directly (the runtime builds attributes by reflection, outside DI), so split it: a
`TypeFilterAttribute` marker plus the actual filter class, which DI resolves normally.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class TeamMemberAttribute : TypeFilterAttribute
{
    public TeamMemberAttribute() : base(typeof(TeamMemberFilter))
    {
    }
}

public class TeamMemberFilter(AppDbContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user is not { Identity.IsAuthenticated: true })
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (user.IsInRole("Admin"))
        {
            return; // global bypass — checked before the DB hit
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!context.RouteData.Values.TryGetValue("teamId", out var teamIdObj)
            || !int.TryParse(teamIdObj?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var teamId))
        {
            context.Result = new ForbidResult();
            return;
        }

        bool isMember = await db.TeamMembers
            .AnyAsync(m => m.UserId == userId && m.TeamId == teamId, context.HttpContext.RequestAborted);

        if (!isMember)
        {
            context.Result = new ForbidResult();
        }
    }
}

// Usage: [HttpGet("{teamId:int}/reports")] [TeamMember]
```

**Membership is 1:1 and set once** (a user belongs to exactly one branch/tenant/org) — don't hit
the DB every request. Embed it as a claim at token issuance instead, the same way a role claim
already is, and the check collapses to a string comparison with no `DbContext` dependency — which
also means no `TypeFilterAttribute` indirection is needed; the attribute can implement the filter
directly:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BranchMemberAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user is not { Identity.IsAuthenticated: true })
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (user.IsInRole("Admin"))
        {
            return;
        }

        if (!context.RouteData.Values.TryGetValue("branchId", out var branchIdObj)
            || user.FindFirstValue("branch_id") != branchIdObj?.ToString())
        {
            context.Result = new ForbidResult();
        }
    }
}
```

Trade-off: a claim goes stale until the next login/token refresh if membership changes mid-session
— acceptable for the same reason it's already accepted for the role claim, not a new risk.

### Protecting Endpoints

```csharp
// Protect an entire group
app.MapGroup("/api/admin")
    .WithTags("Admin")
    .RequireAuthorization("AdminOnly")
    .MapAdminEndpoints();

// Protect individual endpoints
group.MapPost("/", CreateOrder)
    .RequireAuthorization("CanManageOrders");

// Allow anonymous on a protected group
group.MapGet("/public-info", GetPublicInfo)
    .AllowAnonymous();
```

### OpenID Connect (External Identity Provider)

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Oidc:Authority"];
    options.ClientId = builder.Configuration["Oidc:ClientId"];
    options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
});
```

### Accessing Current User

```csharp
// In minimal API handlers — inject ClaimsPrincipal or HttpContext
group.MapGet("/me", (ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    var email = user.FindFirstValue(ClaimTypes.Email);
    return TypedResults.Ok(new { userId, email });
}).RequireAuthorization();
```

## Anti-patterns

### Don't Use Role Strings Everywhere

```csharp
// BAD — magic strings, hard to refactor, not testable
[Authorize(Roles = "Admin,SuperAdmin,Manager")]
public class AdminController { }

// GOOD — policy-based
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminAccess", p => p.RequireRole("Admin", "SuperAdmin", "Manager"));

group.MapGet("/", Handler).RequireAuthorization("AdminAccess");
```

### Don't Store Secrets in appsettings.json

```json
// BAD — committed to source control
{
  "Jwt": {
    "Key": "super-secret-key-12345"
  }
}
```

```bash
# GOOD — use user secrets in development
dotnet user-secrets set "Jwt:Key" "super-secret-key-12345"
```

### Don't Null-Chain Into `Identity.IsAuthenticated`

```csharp
// BAD — doesn't guard against `user` itself being null (no `?.` after `user`),
// and reads as a boolean comparison rather than a null-safety check
if (user?.Identity?.IsAuthenticated == false) { ... }

// GOOD — an extended property pattern (C# 10+) is null-safe at every level:
// matches only when `user`, `user.Identity`, and `IsAuthenticated == true` all hold
if (user is not { Identity.IsAuthenticated: true }) { ... }
```

### Don't Skip Token Validation

```csharp
// BAD — disabling validation
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = false,      // DON'T
    ValidateAudience = false,    // DON'T
    ValidateLifetime = false,    // DEFINITELY DON'T
};

// GOOD — validate everything (see JWT Bearer Authentication pattern above for full setup)
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| REST API | JWT Bearer authentication |
| Blazor Server / MVC | Cookie authentication |
| External identity provider | OpenID Connect |
| User registration / login | ASP.NET Identity |
| Permission checking | Policy-based authorization |
| Multi-tenant API | Claims-based with tenant claim |
| API-to-API communication | Client credentials (OAuth 2.0) |
| Simple API keys | Custom `AuthenticationHandler<T>` |
| Resource-scoped membership (route-bound id) | Custom `IAuthorizationFilter` — `TypeFilterAttribute` + DB check if membership can change at runtime, claims-only if it's 1:1 and set at login |
