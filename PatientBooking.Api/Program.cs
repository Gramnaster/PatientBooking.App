using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Services;
using PatientBooking.Api.Common.Models.Config;
using PatientBooking.Api.Domain;
using PatientBooking.Api.Domain.Security;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using System.Globalization;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File("Logs/log-.txt", formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting the PatientBooking API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
    );

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation())
        .UseOtlpExporter();

    // Add services to the container.
    var connectionString = builder.Configuration.GetConnectionString("PatientBookingDbConnectionString");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Log.Fatal("ConnectionStrings:PatientBookingDbConnectionString is not configured.");
        throw new InvalidOperationException("ConnectionStrings:PatientBookingDbConnectionString is not configured");
    }

    // Adding DBContext to use SQL Server
    builder.Services.AddDbContext<PatientBookingDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("PatientBookingDbConnectionString")));

    // Built-in Minimal API endpoints
    builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
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

    builder.Services.AddAuthentication(options =>
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
        });

    builder.Services.AddAuthorization();

    // Register own business-logic services
    builder.Services.AddScoped<IUsersService, UsersService>();

    // Singletons
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<SmtpIdentityEmailSender>();
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpIdentityEmailSender>());
    builder.Services.AddSingleton<ILoginNotificationSender>(sp => sp.GetRequiredService<SmtpIdentityEmailSender>());

    // PII Encryption Singletons
    builder.Services.Configure<LookupProtectionSettings>(builder.Configuration.GetSection("LookupProtection"));
    builder.Services.AddSingleton<IPersonalDataProtector, PersonalDataProtector>();
    builder.Services.AddSingleton<ILookupProtectorKeyRing, LookupProtectorKeyRing>();
    builder.Services.AddSingleton<ILookupProtector, LookupProtector>();

    // Data Protection's own key ring, persisted to disk so okeys survive app restarts
    var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
    builder.Services.AddDataProtection()
        .SetApplicationName("PatientBooking.Api")
        .PersistKeysToFileSystem(new DirectoryInfo(
            string.IsNullOrWhiteSpace(dataProtectionKeyPath)
            ? Path.Combine(builder.Environment.ContentRootPath, "keys")
            : dataProtectionKeyPath));

    builder.Services.AddHttpClient<IBreachedPasswordChecker, HaveIBeenPwnedPasswordChecker>(client =>
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


    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Identity's built-in endpoints need different prefix or the two will collide
    app.MapGroup("api/defaultauth").MapIdentityApi<ApplicationUser>();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

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
}
finally
{
    await Log.CloseAndFlushAsync();
}