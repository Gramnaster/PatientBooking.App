using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Application.Services;
using PatientBooking.Api.Application.Validators.Clinic;
using PatientBooking.Api.BackgroundServices;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Domain.Security;
using PatientBooking.Api.Filters;
using PatientBooking.Api.Handlers;
using PatientBooking.Api.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel
    .Information()
    .MinimumLevel
    .Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel
    .Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel
    .Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich
    .FromLogContext()
    .Enrich
    .WithSpan()
    .WriteTo
    .Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo
    .File("Logs/log-.txt", formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var migrateOnly = args.Contains("--migrate", StringComparer.Ordinal);
    Log.Information(migrateOnly ? "Applying database migrations..." : "Starting the PatientBooking API...");

    var builder = WebApplication.CreateBuilder(
        args.Where(arg => !string.Equals(arg, "--migrate", StringComparison.Ordinal)).ToArray()
    );

    builder.Host.UseSerilog(
        (context, services, configuration) => configuration
            .ReadFrom
            .Configuration(context.Configuration)
            .ReadFrom
            .Services(services)
            .Enrich
            .FromLogContext()
            .Enrich
            .WithSpan()
            .Enrich
            .WithProperty("Application", builder.Environment.ApplicationName)
    );

    builder
        .Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
        .WithTracing(
            tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
        )
        .WithMetrics(
            metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation()
        )
        .UseOtlpExporter();

    // Add services to the container.
    var connectionString = builder.Configuration.GetConnectionString("PatientBookingDbConnectionString");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Log.Fatal("ConnectionStrings:PatientBookingDbConnectionString is not configured.");
        throw new InvalidOperationException("ConnectionStrings:PatientBookingDbConnectionString is not configured");
    }

    // Adding DBContext to use SQL Server
    builder.Services.AddDbContext<PatientBookingDbContext>(
        options => options.UseSqlServer(builder.Configuration.GetConnectionString("PatientBookingDbConnectionString"))
    );

    // Built-in Minimal API endpoints
    builder
        .Services
        .AddIdentityApiEndpoints<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedEmail = true;

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.AllowedForNewUsers = true;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
        })
        //.AddRoles<IdentityRole>() // Not necessary anymore
        .AddEntityFrameworkStores<PatientBookingDbContext>();

    // Adds JWT as the default scheme
    // WIP: Complete the setup of checking JWTsettings, along the other schemes required
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

    // Enables emailing
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

    // Fail fast if key is missing rather than issuing tokens none can validate
    var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
    if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    {
        throw new InvalidOperationException("JwtSettings:Key is not configured");
    }

    builder
        .Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                ClockSkew = TimeSpan.Zero, // Default is 5 mins
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var db = context.HttpContext.RequestServices.GetRequiredService<PatientBookingDbContext>();
                    if (
                        string.IsNullOrEmpty(userId) ||
                        !await db.Users.AnyAsync(
                            user => user.Id == userId && user.DeletedAtUtc == null,
                            context.HttpContext.RequestAborted
                        )
                    )
                    {
                        context.Fail("Account is unavailable.");
                    }
                },
            };
        });

    builder.Services.Configure<AdminSeedSettings>(builder.Configuration.GetSection("AdminSeed"));

    builder.Services.AddAuthorization();

    // Register own business-logic services
    builder.Services.AddScoped<IUsersService, UsersService>();
    builder.Services.AddScoped<IClinicService, ClinicServices>();
    builder.Services.AddScoped<IBookingService, BookingServices>();
    builder.Services.AddScoped<IEmployeeService, EmployeeService>();

    // Singletons
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<SmtpIdentityEmailSender>();
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(
        sp => sp.GetRequiredService<SmtpIdentityEmailSender>()
    );
    builder.Services.AddSingleton<ILoginNotificationSender>(sp => sp.GetRequiredService<SmtpIdentityEmailSender>());

    // PII Encryption Singletons
    builder.Services.Configure<LookupProtectionSettings>(builder.Configuration.GetSection("LookupProtection"));
    builder.Services.AddSingleton<IPersonalDataProtector, PersonalDataProtector>();
    builder.Services.AddSingleton<ILookupProtectorKeyRing, LookupProtectorKeyRing>();
    builder.Services.AddSingleton<ILookupProtector, LookupProtector>();

    // FluentValidation - One call registers every IValidator<T> in App assembly
    builder.Services.AddValidatorsFromAssemblyContaining<CreateClinicDtoValidator>();

    // RabbitMQ
    builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));

    builder.Services.AddSingleton<RabbitMqConnectionProvider>();
    builder.Services.AddSingleton<IBookingEventPublisher, RabbitMqBookingEventPublisher>();
    builder.Services.AddSingleton<IBookingNotificationSender>(sp => sp.GetRequiredService<SmtpIdentityEmailSender>());
    builder.Services.AddHostedService<BookingConfirmationConsumer>();
    builder.Services.AddHostedService<BookingOutboxDispatcher>();

    builder.Services.AddSingleton<IRegistrationEventPublisher, RabbitMqRegistrationEventPublisher>();
    builder.Services.AddSingleton<IRegistrationNotificationSender>(
        sp => sp.GetRequiredService<SmtpIdentityEmailSender>()
    );
    builder.Services.AddHostedService<RegistrationConfirmationConsumer>();
    builder.Services.AddHostedService<RegistrationOutboxDispatcher>();

    // Data Protection's own key ring, persisted to disk so okeys survive app restarts
    var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
    builder
        .Services
        .AddDataProtection()
        .SetApplicationName("PatientBooking.Api")
        .PersistKeysToFileSystem(
            new DirectoryInfo(
                string.IsNullOrWhiteSpace(dataProtectionKeyPath)
                    ? Path.Combine(builder.Environment.ContentRootPath, "keys")
                    : dataProtectionKeyPath
            )
        );

    builder
        .Services
        .AddHttpClient<IBreachedPasswordChecker, HaveIBeenPwnedPasswordChecker>(client =>
        {
            const string address = "https://api.pwnedpasswords.com/";
            client.BaseAddress = new Uri(address);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(4); // Must be >= 2x AttemptTimeout
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(3);
        });

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Partitioned by client IP - must stay after UseForwardedHeaders so RemoteIpAddress is real, not Traefik's.
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10, // a few excess requests wait for the next window instead of an instant 429
                }
            )
        );

        options.OnRejected = async (context, cancellationToken) =>
        {
            var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            Log.Warning(
                "Rate limit exceeded for {ClientIp} on {RequestPath}.",
                clientIp,
                context.HttpContext.Request.Path.Value
            );

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Rate limit exceeded. Try again shortly.",
            };

            // Token bucket/concurrency limiters don't expose this metadata, only fixed/sliding-window ones.
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var retryAfterSeconds = (int)retryAfter.TotalSeconds;
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture
                );
                problemDetails.Extensions["retryAfterSeconds"] = retryAfterSeconds;
            }

            var problemDetailsService = context
                .HttpContext
                .RequestServices
                .GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = problemDetails,
            });
        };
    });

    builder
        .Services
        .AddControllers(options => options.Filters.Add<ValidationFilter>())
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<BearerSecurityRequirementOperationTransformer>();
    });

    var app = builder.Build();

    if (migrateOnly)
    {
        await using (app)
        {
            await using var migrationScope = app.Services.CreateAsyncScope();
            var migrationDb = migrationScope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();
            await migrationDb.Database.MigrateAsync();
            Log.Information("Database migrations completed successfully.");
        }

        return;
    }

    await using (var scope = app.Services.CreateAsyncScope())
    {
        await AdminBootstrapper.SeedAsync(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>(),
            scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedSettings>>().Value,
            scope.ServiceProvider.GetRequiredService<TimeProvider>(),
            app.Logger
        );
    }

    // First in the pipeline so it wraps every downstream middleware and endpoint
    app.UseExceptionHandler();

    // Identity's built-in endpoints need different prefix or the two will collide
    app.MapGroup("api/defaultauth").ExcludeFromDescription().MapIdentityApi<ApplicationUser>();

    // Configure the HTTP request pipeline.
    // Public in every environment, not just Development - solo project, doubles as API docs.
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Trusts Traefik's forwarded scheme so email links use https:// not http:// - safe since ufw blocks external access to Kestrel's port.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);

    app.UseRateLimiter();

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("PatientBooking API started successfully");

    await app.RunAsync().ConfigureAwait(true);
}
catch (HostAbortedException ex)
{
    Log.Debug(ex, "Some EFCore issue");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Exposes the top-level statements' compiler-generated Program class to WebApplicationFactory<Program> in tests.
public partial class Program
{
    protected Program()
    { }
}
