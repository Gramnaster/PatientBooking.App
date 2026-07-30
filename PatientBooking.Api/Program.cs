using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PatientBooking.Api.Domain;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using System.Globalization;

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

    // Identity Service that's obsolete with AddIdentityApiEndpoints
    //builder.Services.AddIdentityCore<ApplicationUser>(options => { })
    //    .AddRoles<IdentityRole>()
    //    .AddEntityFrameworkStores<PatientBookingDbContext>()

    // Built-in Minimal API endpoints
    builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
        .AddEntityFrameworkStores<PatientBookingDbContext>();

    builder.Services.AddAuthorization();

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