using Diten.Platform.Application;
using Diten.Platform.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
/// <c>AddInfrastructure</c> does. The cost of the choice made here is worth naming plainly: registrations
/// made in Program.cs itself — its own four <c>AddHostedService</c> calls among them — are NOT covered by
/// this file. Everything <c>AddApplication</c> and <c>AddInfrastructure</c> register is.
///
/// ⚠ WHY IT NEEDS A REAL MONGODB, which a composition test has no business needing. Measured 2026-08-31:
/// <c>AddInfrastructure</c> does not merely REGISTER things. Between lines 497 and 549 it runs the entire
/// migration and seed suite inline — <c>LegacySavedViewMigration</c>, <c>EnsureIndexesAsync</c>, every
/// <c>*Seed.EnsureSeededAsync</c> — with <c>.GetAwaiter().GetResult()</c> and OUTSIDE the try/catch that
/// honours <c>MongoDbSettings:AllowStartupWithoutDatabase</c>. (That same suite then runs a SECOND time at
/// the end of the method, inside <c>RunMongoStartupInitialization</c>, where the switch is honoured — which
/// is why a startup log prints <c>PositionAssignmentSeed</c> twice.) So composition cannot be separated from
/// database access by configuration, and the switch that exists for this case is dead for the inline copy.
/// That is a defect in its own right and is reported separately; this test simply cannot pretend otherwise.
///
/// Requiring Mongo is the established convention here rather than a new burden — see
/// <c>MongoIntegrationHarness</c>, whose own comment states the position: "Tests built on this harness
/// deliberately have no skip-if-unavailable escape hatch: a missing Mongo is a broken dev environment, and
/// silently skipping is what let the bug ship." The database name below follows that file's rule for
/// database-global work: a FIXED name, never a Guid, so it is reused rather than accumulated.
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
    {
        // ⚠ WHY ValidateOnBuild AND NOT JUST ValidateScopes, MEASURED RATHER THAN ASSUMED. Resolving
        // IEnumerable<IHostedService> from the root provider with ValidateScopes alone was tried against the
        // reverted (broken) code, and it DOES catch this particular defect — same message. ValidateOnBuild is
        // kept because it is the wider net: it validates EVERY registered descriptor rather than only the
        // ones reachable from a hosted service, so a captive dependency in an ordinary singleton is caught
        // too. It is also exactly what Program.cs configures, which makes this test and the running service
        // ask the same question rather than two similar ones.
        ServiceProvider? provider = null;
        var failure = Record.Exception(() =>
            provider = Composition.Value.BuildServiceProvider(new ServiceProviderOptions
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
    }

    /// <summary>
    /// Composed once per process — see the BSON note in the class summary. Building several providers from
    /// one collection is fine; calling <c>AddInfrastructure</c> more than once in a process is not.
    /// </summary>
    private static readonly Lazy<IServiceCollection> Composition = new(() =>
    {
        var configuration = TestConfiguration();
        var services = new ServiceCollection();

        // What the HOST always supplies, and therefore not a copy of Program.cs: WebApplicationBuilder
        // registers the configuration and the logging services before any AddX of ours runs.
        services.AddLogging();
        services.AddSingleton(configuration);

        services.AddApplication();
        services.AddInfrastructure(configuration, new ContainerValidationHostEnvironment());

        // ⚠ THE TWO THINGS THE WEB LAYER SUPPLIES THAT INFRASTRUCTURE SERVICES DEPEND ON. Measured: without
        // these, ValidateOnBuild reports seventeen errors that are not defects — IActorPermissionContext
        // (needed by TaskWorkItemProvider, TaskFieldDefinitionService and several Task handlers) and
        // EndpointDataSource (needed by AuthorizationPolicyCache).
        //
        // The first is Program.cs's own registration, reproduced. The second is an EMPTY data source rather
        // than AddControllers(): measured, AddControllers() drags in the whole MVC subsystem, which cannot be
        // constructed outside a web host at all (IWebHostEnvironment, ControllerRequestDelegateFactory), and
        // that produced a fresh set of failures about MVC rather than about this service. Routing is not what
        // is under test here; the lifetimes of what Application and Infrastructure register are.
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
        services.AddScoped<Diten.Platform.Application.Contracts.IActorPermissionContext,
            Diten.Platform.API.Security.ClaimsActorPermissionContext>();

        return services;
    });

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
    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .AddJsonFile(ApiSettingsPath("appsettings.json"), optional: false)
            .AddJsonFile(ApiSettingsPath("appsettings.Development.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A database of this test's own, under a FIXED name so it is reused and cannot pile up —
                // MongoIntegrationHarness.CreateIsolatedAsync's rule, for the same reason: what
                // AddInfrastructure seeds is database-global, not tenant-scoped.
                ["MongoDbSettings:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDbSettings:DatabaseName"] = "diten_platform_itest_container_validation",
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

                // ⚠ BackgroundJobs IS NOT OVERRIDDEN. appsettings.Development.json enables it, and that is
                // load-bearing: Hangfire is what registers IBackgroundJobScheduler, which EmailDispatchSweepJob
                // needs. Switching it off here would make the guard red for a reason the running service does
                // not have.
                ["Smtp:Enabled"] = "false"
            })
            .Build();

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
