using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.Application;
using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

/// <summary>
/// Does the container this service composes actually build?
///
/// ⚠ WHY THIS EXISTS. Measured 2026-08-31: Platform would not start at all — not slowly, not degraded, not
/// at all. <c>SubscriptionPlanStartupInitializer</c> is an <see cref="IHostedService"/>, and a hosted service
/// is registered as a SINGLETON. It took <c>IMongoDatabase</c> as a constructor argument, and
/// <c>IMongoDatabase</c> is registered SCOPED (Infrastructure/DependencyInjection.cs). The container refused
/// to build and the process died at <c>builder.Build()</c>:
///
///     Cannot consume scoped service 'MongoDB.Driver.IMongoDatabase'
///     from singleton 'Microsoft.Extensions.Hosting.IHostedService'.
///
/// NOT ONE of roughly 1500 passing tests saw it, and no additional test ABOUT SUBSCRIPTION PLANS would have.
/// The check that catches this runs when the CONTAINER IS BUILT, and a unit test never builds the container —
/// it news up the class under test and hands it fakes, which is exactly the step that skips the lifetime
/// rule. The only thing exercising it was a human starting the app by hand.
///
/// Program.cs asks for the check explicitly, and unconditionally — not only in Development:
///
///     builder.Host.UseDefaultServiceProvider((_, options) =>
///     {
///         options.ValidateOnBuild = true;
///         options.ValidateScopes = true;
///     });
///
/// This test performs that same validation, with the same two flags, over the same composition, so the next
/// captive dependency is a red test instead of a service that will not boot.
///
/// ⚠ WHY NOT <c>WebApplicationFactory&lt;Program&gt;</c>, which would cover strictly more. Measured this
/// session: it cannot work here. Program.cs calls <c>AddInfrastructure(builder.Configuration, …)</c> at line
/// 76 and <c>builder.Build()</c> at line 221, and under minimal hosting a factory's
/// <c>ConfigureAppConfiguration</c> is applied during <c>Build()</c> — i.e. AFTER the configuration under
/// test has already been read and acted upon. A WAF-based guard therefore cannot influence what
/// <c>AddInfrastructure</c> does. The cost of the choice made here is worth naming plainly: registrations made
/// in Program.cs itself are NOT covered by this file. The API-layer startup-maintenance registrations have their
/// own production-helper test in <c>PlatformApiStartupExecutionModeTests</c>. Everything <c>AddApplication</c>
/// and <c>AddInfrastructure</c> register is covered here.
///
/// ⚠ WHY IT NEEDS A REAL MONGODB, which a composition test has no business needing. Measured 2026-08-31:
/// <c>AddInfrastructure</c> does not merely REGISTER things in its default mode. It runs the migration and seed
/// suites inline and then runs the guarded startup initialization chain. The Development-only API-serving mode
/// can now omit those operations explicitly, but this test deliberately composes the DEFAULT mode: it is the
/// regression guard that an argument-free normal startup still builds under the production validation rules.
///
/// Requiring Mongo is the established convention here rather than a new burden — see
/// <c>MongoIntegrationHarness</c>, whose own comment states the position: "Tests built on this harness
/// deliberately have no skip-if-unavailable escape hatch: a missing Mongo is a broken dev environment, and
/// silently skipping is what let the bug ship." This test instead starts a dynamic-loopback, test-owned Mongo
/// process and deletes its whole data directory, so the default maintenance regression never reaches the
/// application's Local Development database.
///
/// ⚠ AND WHY THIS FILE LIVES IN THE BACKGROUND-JOBS TEST PROJECT rather than beside the other Platform
/// tests. <c>AddInfrastructure</c> calls <c>BsonSerializer.RegisterSerializer</c>, which writes to a
/// PROCESS-GLOBAL registry and THROWS if the type is already registered. Diten.Platform.Application.Tests
/// registers those same serializers from a <c>[ModuleInitializer]</c> before its first test runs, so
/// composing there fails with "There is already a serializer registered for type Guid" — red, but about the
/// wrong thing. This project registers none, and hosted-service lifetime is its subject anyway. The same
/// global registry is why the composition below is built ONCE per process behind a <c>Lazy</c>: a second
/// <c>AddInfrastructure</c> call in one process would hit that registry again, so any further test added
/// here must share this composition rather than compose its own.
///
/// ⚠ THIS TEST WAS PROVED TO FAIL, not merely observed to pass. With the constructor parameter put back, it
/// reports the production message verbatim — "Cannot consume scoped service 'MongoDB.Driver.IMongoDatabase'
/// from singleton 'Microsoft.Extensions.Hosting.IHostedService'" — and it passes with the parameter removed.
/// A guard that has only ever been green is not known to guard anything.
/// </summary>
public sealed class PlatformContainerValidationTests
{
    [Fact]
    public async Task Platform_container_builds_under_the_validation_the_app_boots_with()
        => await ValidateContainerAsync(runStartupMaintenance: true, schedulerEnabled: true);

    [Fact]
    public async Task Api_serving_container_builds_with_scheduler_and_dashboard_disabled()
        => await ValidateContainerAsync(runStartupMaintenance: false, schedulerEnabled: false);

    private static async Task ValidateContainerAsync(bool runStartupMaintenance, bool schedulerEnabled)
    {
        await using var mongo = await OwnedMongoProcess.StartAsync();
        var configuration = TestConfiguration(
            mongo.ConnectionString,
            "diten_platform_itest_container_validation",
            schedulerEnabled);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddApplication();
        services.AddInfrastructure(
            configuration,
            new ContainerValidationHostEnvironment(),
            runStartupMaintenance: runStartupMaintenance);
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
        services.AddScoped<Diten.Platform.Application.Contracts.IActorPermissionContext,
            Diten.Platform.API.Security.ClaimsActorPermissionContext>();
        services.Configure<TrustedWorkflowStartAuthorizationOptions>(
            configuration.GetSection(TrustedWorkflowStartAuthorizationOptions.SectionName));
        services.AddSingleton<IOrgDataScopeCandidateAvailabilityClassifier,
            MongoOrgDataScopeCandidateAvailabilityClassifier>();
        services.AddSingleton<ConfiguredTrustedWorkflowStartAuthorizationPolicy>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<TrustedWorkflowStartAuthorizationOptions>,
            TrustedWorkflowStartAuthorizationOptionsValidator>();
        services.AddSingleton<ITrustedWorkflowStartAuthorizationPolicy>(provider =>
            provider.GetRequiredService<ConfiguredTrustedWorkflowStartAuthorizationPolicy>());

        // ⚠ WHY ValidateOnBuild AND NOT JUST ValidateScopes, MEASURED RATHER THAN ASSUMED. Resolving
        // IEnumerable<IHostedService> from the root provider with ValidateScopes alone was tried against the
        // reverted (broken) code, and it DOES catch this particular defect — same message. ValidateOnBuild is
        // kept because it is the wider net: it validates EVERY registered descriptor rather than only the
        // ones reachable from a hosted service, so a captive dependency in an ordinary singleton is caught
        // too. It is also exactly what Program.cs configures, which makes this test and the running service
        // ask the same question rather than two similar ones.
        ServiceProvider? provider = null;
        var failure = Record.Exception(() =>
            provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }));

        // DisposeAsync, not Dispose: MassTransitHostedService implements only IAsyncDisposable, and the
        // synchronous path throws over it — which would mask the assertion below with an unrelated failure.
        if (provider is not null)
        {
            await provider.DisposeAsync();
        }

        Assert.True(
            failure is null,
            "The Platform DI container does not build. This is the same validation the service performs at "
            + "startup, so the service will not start either.\n\n" + failure);

        Assert.Equal(schedulerEnabled, services.Any(descriptor =>
            descriptor.ServiceType == typeof(IBackgroundJobScheduler)));
        Assert.Equal(schedulerEnabled, services.Any(descriptor =>
            descriptor.ServiceType == typeof(EmailDispatchSweepJob)));
        if (schedulerEnabled)
        {
            Assert.Equal(ServiceLifetime.Scoped, services.Single(descriptor =>
                descriptor.ServiceType == typeof(EmailDispatchSweepJob)).Lifetime);
        }
    }

    [Fact]
    public void AddInfrastructure_defaults_to_running_startup_maintenance()
    {
        var overload = typeof(Diten.Platform.Infrastructure.DependencyInjection).GetMethods()
            .Single(method =>
                method.Name == nameof(Diten.Platform.Infrastructure.DependencyInjection.AddInfrastructure)
                && method.GetParameters().Length == 4);
        var maintenanceParameter = overload.GetParameters()[3];

        Assert.True(maintenanceParameter.HasDefaultValue);
        Assert.True(Assert.IsType<bool>(maintenanceParameter.DefaultValue));
    }

    /// <summary>
    /// The service's OWN configuration files, with only what this test must pin layered on top.
    ///
    /// ⚠ IT READS THE REAL FILES ON PURPOSE. <c>AddInfrastructure</c> throws outright on a missing section —
    /// <c>AuditRetentionSeed</c>, <c>WorkAggregation</c>, <c>MessagingProviders</c> and others — so a
    /// hand-written configuration here would be a second copy of appsettings.json that nobody updates: every
    /// section a future change adds would be missing from it, and this guard would then fail for a
    /// configuration reason having nothing to do with lifetimes — noise that teaches people to ignore it.
    /// Reading the shipped files means this test sees the same configuration surface the service does.
    /// </summary>
    private static IConfiguration TestConfiguration(
        string mongoConnectionString, string databaseName, bool schedulerEnabled) =>
        new ConfigurationBuilder()
            .AddJsonFile(ApiSettingsPath("appsettings.json"), optional: false)
            .AddJsonFile(ApiSettingsPath("appsettings.Development.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A database inside the dynamic-loopback, test-owned mongod. The whole data directory is
                // removed after the test; the Local Development Mongo service is never contacted.
                ["MongoDbSettings:ConnectionString"] = mongoConnectionString,
                ["MongoDbSettings:DatabaseName"] = databaseName,
                ["MongoDbSettings:AllowStartupWithoutDatabase"] = "true",

                // Secrets the infrastructure layer refuses to compose without. Local-only literals: nothing
                // is signed or authenticated with them, because nothing is started.
                ["JwtSettings:Secret"] = "container-validation-only-jwt-signing-secret-0123456789",
                ["JwtSettings:Issuer"] = "diten-platform-tests",
                ["JwtSettings:Audience"] = "diten-platform-tests",
                ["AuthService:BaseUrl"] = "http://localhost:5001",
                ["AuthService:InternalApiKey"] = "container-validation-only-internal-api-key",
                ["ModuleRegistrationCredentials:Mdm:Identifier"] = "ditenmdmservice",
                ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] =
                    "container-validation-only-module-registration-secret",

                ["BackgroundJobs:Enabled"] = schedulerEnabled.ToString(),
                ["BackgroundJobs:DashboardEnabled"] = "false",
                ["Smtp:Enabled"] = "false"
            })
            .Build();

    private sealed class OwnedMongoProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _root;

        private OwnedMongoProcess(Process process, string root, int port)
        {
            _process = process;
            _root = root;
            ConnectionString = $"mongodb://127.0.0.1:{port}/?directConnection=true";
        }

        public string ConnectionString { get; }

        public static async Task<OwnedMongoProcess> StartAsync()
        {
            var binary = new[]
                {
                    Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD"),
                    @"C:\Program Files\MongoDB\Server\8.0\bin\mongod.exe",
                    @"C:\Program Files\MongoDB\Server\7.0\bin\mongod.exe",
                    @"C:\Program Files\MongoDB\Server\6.0\bin\mongod.exe",
                    "/opt/homebrew/bin/mongod",
                    "/usr/local/bin/mongod",
                    "/opt/local/bin/mongod"
                }
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                ?? throw new InvalidOperationException(
                    "A local mongod binary is required; this test never uses the application MongoDB.");
            var port = ReservePort();
            var root = Path.Combine(
                Path.GetTempPath(),
                "diten-platform-container-validation-" + Guid.NewGuid().ToString("N"));
            var data = Path.Combine(root, "data");
            Directory.CreateDirectory(data);
            var start = new ProcessStartInfo
            {
                FileName = binary,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var arguments = new List<string>
            {
                "--dbpath", data,
                "--port", port.ToString(),
                "--bind_ip", "127.0.0.1",
                "--quiet"
            };
            if (!OperatingSystem.IsWindows())
            {
                arguments.Add("--nounixsocket");
            }

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            var process = Process.Start(start)
                          ?? throw new InvalidOperationException("Failed to start the test-owned mongod process.");
            var settings = MongoClientSettings.FromConnectionString(
                $"mongodb://127.0.0.1:{port}/?directConnection=true");
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
            var client = new MongoClient(settings);
            try
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException(
                            $"Test-owned mongod exited with code {process.ExitCode}.");
                    }

                    try
                    {
                        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                            new BsonDocument("ping", 1));
                        return new OwnedMongoProcess(process, root, port);
                    }
                    catch (Exception exception) when (exception is MongoException or TimeoutException)
                    {
                        await Task.Delay(100);
                    }
                }

                throw new TimeoutException("Test-owned mongod did not become ready.");
            }
            catch
            {
                Stop(process);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }

                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            Stop(_process);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }

        private static int ReservePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static void Stop(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// A settings file inside Diten.Platform.API, found by WALKING UP to the AGENTS.md marker rather than by
    /// counting directories out of the build output. Same reasoning as
    /// Diten.Platform.Application.Tests.RepoPaths: a fixed number of "../" is right in exactly one checkout
    /// shape, and AGENTS.md is a tracked FILE, so it is found in a git worktree too — where <c>.git</c> is a
    /// file rather than a directory and a <c>Directory.Exists</c> probe walks off the top of the filesystem.
    /// </summary>
    private static string ApiSettingsPath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                $"Repo root not found above '{AppContext.BaseDirectory}' — no AGENTS.md on any parent.");
        }

        return Path.Combine(
            current.FullName, "services", "Diten.Platform", "src", "Diten.Platform.API", fileName);
    }

    private sealed class ContainerValidationHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Diten.Platform.API";
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
