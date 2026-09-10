using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatientBooking.Api.Application.DTOs.Auth;
using PatientBooking.Api.Domain;
using Testcontainers.MsSql;
using Xunit;

namespace PatientBooking.Api.Tests.Infrastructure;

// Full HTTP pipeline (real routing, JWT auth, [Authorize(Roles = "Patient")], and the real Data
// Protection stack Program.cs wires up) against one shared SQL Server container - not
// MessagingTestFixture's manual ServiceCollection host, and not its passthrough protectors, because
// the booking cancellation flow's response contract and encrypted-name projection need verifying
// the way a real client sees them: through routing, role authorization, and genuine ciphertext at
// rest, not a hand-assembled service call.
public sealed class BookingEndpointTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string PatientPassword = "P@ssw0rd1234!";

    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string dataProtectionKeyPath = Path.Combine(Path.GetTempPath(), $"pb-dp-{Guid.NewGuid():N}");

    public int ClinicId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await sqlContainer.StartAsync();
        await MigrateDatabaseAsync();
        await SeedClinicAsync();

        // Forces the host to build now (AdminBootstrapper.SeedAsync, hosted services, etc.) so a
        // startup failure surfaces here rather than inside the first test.
        using HttpClient warmup = CreateClient();
    }

    // IAsyncLifetime : IAsyncDisposable, so this override satisfies both - WebApplicationFactory
    // already implements IAsyncDisposable.DisposeAsync() virtually, and must still run to tear down
    // the in-process TestServer/host.
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(dataProtectionKeyPath))
        {
            Directory.Delete(dataProtectionKeyPath, recursive: true);
        }

        await sqlContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development loads user secrets, so the real JwtSettings/LookupProtection keys already
        // configured for local dev apply here too - only the values below need overriding for an
        // isolated test run against a throwaway container.
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyPath"] = dataProtectionKeyPath,

                // A fresh container has no admin yet, and AdminBootstrapper throws if AdminSeed:Email
                // is set without a bootstrap password. Admin seeding is unrelated to this flow.
                ["AdminSeed:Email"] = string.Empty,
            });
        });

        // EF Core's compiled-model cache is keyed by DbContext type by default, not by which
        // IPersonalDataProtector/ILookupProtector instance built it - running in the same process as
        // MessagingTestFixture's passthrough-protector host risks reusing a model whose value
        // converters were baked from THAT no-op protector. EnableServiceProviderCaching(false) forces
        // a fresh model here, matching the same workaround BookingProjectionTests.cs already uses.
        builder.ConfigureServices(services =>
        {
            ServiceDescriptor? descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PatientBookingDbContext>)
            );
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PatientBookingDbContext>(
                options => options.UseSqlServer(sqlContainer.GetConnectionString()).EnableServiceProviderCaching(false)
            );
        });
    }

    // Registers via the real endpoint, confirms the email directly (bypassing the "click the link"
    // step - already covered by the separate email-flow review), logs in via the real endpoint, and
    // returns a ready-to-use Bearer client plus the caller's user id.
    public async Task<(HttpClient Client, string UserId)> CreatePatientClientAsync(
        string firstName,
        string lastName,
        CancellationToken ct
    )
    {
        HttpClient client = CreateClient();
        string email = $"{Guid.NewGuid():N}@patientbooking.test";

        RegisterUserDto registerDto = new()
        {
            Email = email,
            Password = PatientPassword,
            FirstName = firstName,
            LastName = lastName,
        };
        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/Auth/register", registerDto, ct);
        registerResponse.EnsureSuccessStatusCode();
        RegisteredUserDto registered = (await registerResponse.Content.ReadFromJsonAsync<RegisteredUserDto>(ct))!;

        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = await userManager.FindByIdAsync(registered.Id)
                ?? throw new InvalidOperationException("Registered user not found.");
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        LoginUserDto loginDto = new() { Email = email, Password = PatientPassword };
        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/Auth/login", loginDto, ct);
        loginResponse.EnsureSuccessStatusCode();
        LoginResponseDto login = (await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>(ct))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return (client, registered.Id);
    }

    // A fresh DbContext against the same container - used only for raw scalar SQL reads (bypassing
    // entity materialization and its value converters entirely) to inspect what's actually stored,
    // e.g. confirming a name column is genuinely ciphertext. The passthrough protectors are never
    // exercised by a raw SqlQuery<T> projection, so which implementation is registered here doesn't
    // matter for that use.
    public PatientBookingDbContext CreateRawDbContext()
    {
        DbContextOptions<PatientBookingDbContext> options = new DbContextOptionsBuilder<PatientBookingDbContext>()
            .UseSqlServer(sqlContainer.GetConnectionString())
            .Options;
        return new PatientBookingDbContext(options, new PassthroughPersonalDataProtector(), new PassthroughLookupProtector());
    }

    private async Task MigrateDatabaseAsync()
    {
        await using PatientBookingDbContext db = CreateRawDbContext();
        await db.Database.MigrateAsync();
    }

    // One clinic, open every hour of every day, shared by every test in the collection - simplest
    // way to avoid each test needing its own operating-hours setup. Tests avoid colliding with each
    // other via distinct appointment days, not distinct clinics.
    private async Task SeedClinicAsync()
    {
        await using PatientBookingDbContext db = CreateRawDbContext();

        Clinic clinic = new() { Name = "Endpoint Test Clinic", Address = "1 Test Way" };
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            clinic.OperatingHours.Add(new ClinicOperatingHours
            {
                DayOfWeek = day,
                OpenTime = TimeOnly.MinValue,
                CloseTime = TimeOnly.MaxValue,
            });
        }

        db.Clinics.Add(clinic);
        await db.SaveChangesAsync();
        ClinicId = clinic.Id;
    }
}
