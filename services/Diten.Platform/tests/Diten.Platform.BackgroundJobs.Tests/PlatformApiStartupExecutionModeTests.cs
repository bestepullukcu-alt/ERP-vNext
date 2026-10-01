using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.API.Services.ModuleRegistration;
using Diten.Platform.API.Services.Security;
using Diten.Platform.Infrastructure;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Services.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class PlatformApiStartupExecutionModeTests
{
    private const string PipelinePolicy = "startup-mode-test-permission-policy";
    private const string RequiredPermission = "platform.startup-mode.test";
    private const string TestAuthenticationScheme = "StartupModePipelineTest";
    private const string AuthenticateHeader = "X-Test-Authenticate";
    private const string TenantClaimHeader = "X-Test-Tenant-Claim";
    private const string PermissionClaimHeader = "X-Test-Permission-Claim";

    [Fact]
    public void No_argument_preserves_normal_startup_maintenance_and_api_worker_registrations()
    {
        var mode = ApiStartupExecutionMode.Resolve([], null, null);
        var services = new ServiceCollection();

        mode.AddApiStartupMaintenanceServices(services);

        Assert.True(mode.RunStartupMaintenance);
        AssertHostedService<BusinessReferenceDataCatalogLoadWorker>(services);
        AssertHostedService<VerifiedGskuOperationalProvisioningRunner>(services);
        AssertHostedService<PlatformModuleSelfRegistrationWorker>(services);
        AssertHostedService<PlatformPermissionAutoRegistrationWorker>(services);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ModuleSelfRegistrationGate));
    }

    [Fact]
    public void Exact_development_argument_omits_api_startup_maintenance_workers()
    {
        var mode = ApiStartupExecutionMode.Resolve(
            [ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument],
            Environments.Development,
            Environments.Development);
        var services = new ServiceCollection();

        mode.AddApiStartupMaintenanceServices(services);

        Assert.False(mode.RunStartupMaintenance);
        Assert.Empty(services);
        mode.ValidateResolvedEnvironment(Environments.Development);
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public void Invalid_or_conflicting_arguments_fail_closed_before_composition(string[] arguments)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApiStartupExecutionMode.Resolve(
                arguments,
                Environments.Development,
                Environments.Development));

        Assert.StartsWith("API_STARTUP_EXECUTION_MODE_", exception.Message);
    }

    [Theory]
    [InlineData("Production", "Development")]
    [InlineData("Development", "Staging")]
    [InlineData(null, null)]
    public void Non_development_or_unspecified_environment_fails_closed(
        string? aspNetCoreEnvironment,
        string? dotNetEnvironment)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApiStartupExecutionMode.Resolve(
                [ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument],
                aspNetCoreEnvironment,
                dotNetEnvironment));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_ENVIRONMENT_NOT_ALLOWED", exception.Message);
    }

    [Fact]
    public void Command_line_environment_must_not_mask_a_non_development_process_environment()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApiStartupExecutionMode.Resolve(
                [
                    ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument,
                    "--environment",
                    Environments.Development
                ],
                Environments.Production,
                null));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_ENVIRONMENT_NOT_ALLOWED", exception.Message);
    }

    [Fact]
    public void Resolved_host_environment_is_checked_again_before_infrastructure_composition()
    {
        var mode = ApiStartupExecutionMode.Resolve(
            [ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument],
            Environments.Development,
            Environments.Development);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            mode.ValidateResolvedEnvironment(Environments.Production));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_ENVIRONMENT_NOT_ALLOWED", exception.Message);
    }

    [Fact]
    public void Command_line_gate_runs_before_other_operational_dispatch_and_host_construction()
    {
        var program = File.ReadAllText(RepoFile(
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));

        var gate = program.IndexOf("ApiStartupExecutionMode.Resolve(args)", StringComparison.Ordinal);
        var marketOperation = program.IndexOf("VerifiedMarketOperationalMode.TryRunAsync(args)", StringComparison.Ordinal);
        var hostConstruction = program.IndexOf("WebApplication.CreateBuilder(args)", StringComparison.Ordinal);
        var infrastructureComposition = program.IndexOf("AddInfrastructure(", StringComparison.Ordinal);

        Assert.True(gate >= 0);
        Assert.True(gate < marketOperation);
        Assert.True(marketOperation < hostConstruction);
        Assert.True(hostConstruction < infrastructureComposition);
    }

    [Fact]
    public void Normal_api_does_not_register_the_isolated_market_runner_but_cli_composition_does()
    {
        var program = File.ReadAllText(RepoFile(
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));
        var operationalMode = File.ReadAllText(RepoFile(
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Services",
            "BusinessReferenceData", "VerifiedMarketOperationalMode.cs"));

        Assert.DoesNotContain(
            "AddScoped<VerifiedMarketOperationalProvisioningRunner>()",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "AddScoped<VerifiedMarketOperationalProvisioningRunner>()",
            operationalMode,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_api_mode_creates_no_startup_maintenance_state_in_test_owned_mongo()
    {
        await using var mongo = await OwnedMongoProcess.StartAsync();
        var databaseName = "diten_platform_itest_api_without_startup_maintenance";
        var database = mongo.Client.GetDatabase(databaseName);
        var before = await ReadDatabaseManifestAsync(database);
        Assert.Empty(before);

        var configuration = StartupModeConfiguration(mongo.ConnectionString, databaseName);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(
            configuration,
            new TestHostEnvironment(),
            runStartupMaintenance: false);

        var after = await ReadDatabaseManifestAsync(database);
        Assert.Equal(before, after);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBackgroundJobScheduler));
        AssertHostedService<AuditOutboxWorker>(services);
        AssertHostedService<OutboxPublisherWorker>(services);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(SubscriptionPlanStartupInitializer));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(HangfireRecurringJobRegistrationHostedService));
    }

    [Fact]
    public async Task Real_pipeline_returns_401_for_unauthenticated_request_with_valid_tenant_signal()
    {
        var tenant = Guid.NewGuid();

        var result = await ExecuteProtectedPipelineAsync(
            tenantHeader: tenant,
            authenticatedTenantClaim: null,
            permission: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.False(result.TerminalReached);
    }

    [Fact]
    public async Task Actual_api_host_serves_health_with_scheduler_and_dashboard_disabled()
    {
        await using var mongo = await OwnedMongoProcess.StartAsync();
        var databaseName = "diten_platform_itest_disabled_jobs_host";
        var database = mongo.Client.GetDatabase(databaseName);
        Assert.Empty(await ReadDatabaseManifestAsync(database));
        var apiDirectory = RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API");
        var assembly = Environment.GetEnvironmentVariable("DITEN_PLATFORM_TEST_API_DLL")
            ?? Path.Combine(apiDirectory, "bin", "Release", "net8.0", "Diten.Platform.API.dll");
        Assert.True(File.Exists(assembly), "Build the Platform Release API before running its child-host test.");
        var port = ReservePort();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = apiDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument);
        start.Environment["DOTNET_ENVIRONMENT"] = Environments.Development;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = Environments.Development;
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        foreach (var entry in StartupModeConfiguration(mongo.ConnectionString, databaseName).AsEnumerable())
        {
            if (entry.Value is not null)
                start.Environment[entry.Key.Replace(":", "__", StringComparison.Ordinal)] = entry.Value;
        }
        start.Environment["BackgroundJobs__Enabled"] = "false";
        start.Environment["BackgroundJobs__DashboardEnabled"] = "false";
        start.Environment["BusinessReferenceData__Provider__ReferenceTenantId"] =
            "00000000-0000-0000-0000-000000000001";
        using var process = new Process { StartInfo = start };
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var ready = false;
            var lastHealth = "No HTTP response";
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (!process.HasExited && DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    using var response = await client.GetAsync($"http://127.0.0.1:{port}/health/ready");
                    var body = await response.Content.ReadAsStringAsync();
                    lastHealth = $"HTTP {(int)response.StatusCode}: {body}";
                    using var document = System.Text.Json.JsonDocument.Parse(body);
                    ready = response.StatusCode == HttpStatusCode.OK
                        && document.RootElement
                            .GetProperty("status").GetString() == "Healthy";
                    if (ready) break;
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
                {
                    // Bounded readiness poll; no fallback port or external service.
                }
                await Task.Delay(100);
            }
            if (!ready)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail($"Child host was not Healthy: {lastHealth}\n{await stderr}\n{await stdout}");
            }
            Assert.False(process.HasExited);
            Assert.Empty(await ReadDatabaseManifestAsync(database));
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
        }
    }

    [Fact]
    public async Task Real_pipeline_returns_403_for_same_tenant_authenticated_request_without_permission()
    {
        var tenant = Guid.NewGuid();

        var result = await ExecuteProtectedPipelineAsync(
            tenantHeader: tenant,
            authenticatedTenantClaim: tenant,
            permission: null);

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.False(result.TerminalReached);
    }

    [Fact]
    public async Task Real_pipeline_rejects_conflicting_tenant_before_authorized_terminal()
    {
        var result = await ExecuteProtectedPipelineAsync(
            tenantHeader: Guid.NewGuid(),
            authenticatedTenantClaim: Guid.NewGuid(),
            permission: RequiredPermission);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.False(result.TerminalReached);
    }

    [Fact]
    public async Task Real_pipeline_reaches_terminal_for_same_tenant_authenticated_permitted_request()
    {
        var tenant = Guid.NewGuid();

        var result = await ExecuteProtectedPipelineAsync(
            tenantHeader: tenant,
            authenticatedTenantClaim: tenant,
            permission: RequiredPermission);

        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.True(result.TerminalReached);
    }

    public static TheoryData<string[]> InvalidArgumentCases => new()
    {
        { new[]
            {
                ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument,
                ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument
            }
        },
        { new[] { "--SERVE-API-WITHOUT-STARTUP-MAINTENANCE" } },
        { new[] { "--serve-api-without-startup-maintenance=true" } },
        { new[]
            {
                ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument,
                "--run-verified-market-provisioning"
            }
        },
        { new[]
            {
                ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument,
                "--RUN-AUDIT-OUTBOX-TEMPORAL-STORAGE-MIGRATION"
            }
        }
    };

    private static void AssertHostedService<TImplementation>(IEnumerable<ServiceDescriptor> services) =>
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService)
                          && descriptor.ImplementationType == typeof(TImplementation));

    private static async Task<PipelineResult> ExecuteProtectedPipelineAsync(
        Guid tenantHeader,
        Guid? authenticatedTenantClaim,
        string? permission)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services
            .AddAuthentication(TestAuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, PipelineAuthenticationHandler>(
                TestAuthenticationScheme,
                _ => { });
        services.AddAuthorization(options => options.AddPolicy(
            PipelinePolicy,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", RequiredPermission)));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var application = new ApplicationBuilder(provider);
        application.UseAuthentication();
        application.UseMiddleware<TenantResolutionMiddleware>();
        application.UseAuthorization();

        var terminalReached = false;
        application.Run(context =>
        {
            terminalReached = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var pipeline = application.Build();

        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = "/api/platform/navigation/startup-mode-pipeline-test";
        context.Request.Headers["X-Tenant-Id"] = tenantHeader.ToString();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute { Policy = PipelinePolicy }),
            "startup-mode protected endpoint"));

        if (authenticatedTenantClaim.HasValue)
        {
            context.Request.Headers[AuthenticateHeader] = "true";
            context.Request.Headers[TenantClaimHeader] = authenticatedTenantClaim.Value.ToString();
            context.Request.Headers["X-Test-Actor-Type"] = "tenant_user";
        }

        if (!string.IsNullOrWhiteSpace(permission))
        {
            context.Request.Headers[PermissionClaimHeader] = permission;
        }

        await pipeline(context);
        return new PipelineResult(context.Response.StatusCode, terminalReached);
    }

    private static string RepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Repository root was not found above the test output directory.");
        }

        return Path.Combine([current.FullName, .. segments]);
    }

    private static async Task<string[]> ReadDatabaseManifestAsync(IMongoDatabase database) =>
        (await database.ListCollectionNames().ToListAsync())
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static IConfiguration StartupModeConfiguration(string mongoConnectionString, string databaseName) =>
        new ConfigurationBuilder()
            .AddJsonFile(
                RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.json"),
                optional: false)
            .AddJsonFile(
                RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.Development.json"),
                optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = mongoConnectionString,
                ["MongoDbSettings:DatabaseName"] = databaseName,
                ["JwtSettings:Secret"] = "startup-mode-test-only-jwt-signing-secret-0123456789",
                ["JwtSettings:Issuer"] = "diten-platform-startup-mode-tests",
                ["JwtSettings:Audience"] = "diten-platform-startup-mode-tests",
                ["AuthService:BaseUrl"] = "http://127.0.0.1:1",
                ["AuthService:InternalApiKey"] = "startup-mode-test-only-internal-api-key",
                ["ModuleRegistrationCredentials:Mdm:Identifier"] = "ditenmdmservice",
                ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] =
                    "startup-mode-test-only-module-registration-secret",
                ["BackgroundJobs:Enabled"] = "true",
                ["BackgroundJobs:DashboardEnabled"] = "false",
                ["Eventing:Transport"] = "InMemory",
                ["Eventing:WorkerEnabled"] = "false",
                ["Smtp:Enabled"] = "false"
            })
            .Build();

    private sealed class OwnedMongoProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _root;

        private OwnedMongoProcess(Process process, string root, int port, MongoClient client)
        {
            _process = process;
            _root = root;
            Client = client;
            ConnectionString = $"mongodb://127.0.0.1:{port}/?directConnection=true";
        }

        public MongoClient Client { get; }
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
                    "A local mongod binary is required; the startup-mode test never downloads MongoDB.");
            var port = ReservePort();
            var root = Path.Combine(
                Path.GetTempPath(),
                "diten-platform-api-startup-mode-" + Guid.NewGuid().ToString("N"));
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
                        return new OwnedMongoProcess(process, root, port, client);
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
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Diten.Platform.API";
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class PipelineAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(AuthenticateHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("tenant_id", Request.Headers[TenantClaimHeader].ToString()),
                new("actor_type", Request.Headers["X-Test-Actor-Type"].ToString())
            };
            var permission = Request.Headers[PermissionClaimHeader].ToString();
            if (!string.IsNullOrWhiteSpace(permission))
            {
                claims.Add(new Claim("permission", permission));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private readonly record struct PipelineResult(int StatusCode, bool TerminalReached);

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
