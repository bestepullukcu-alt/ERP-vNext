using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.MdmService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ApiStartupExecutionModeTests
{
    private const string RequiredPermission = "mdm.startup-mode.test";
    private const string TestAuthenticationScheme = "StartupModePipelineTest";
    private const string AuthenticateHeader = "X-Test-Authenticate";
    private const string TenantClaimHeader = "X-Test-Tenant-Claim";
    private const string PermissionClaimHeader = "X-Test-Permission-Claim";

    [Fact]
    public void No_argument_preserves_normal_startup_maintenance()
    {
        var mode = ApiStartupExecutionMode.Parse([]);

        Assert.True(mode.RunStartupMaintenance);
        mode.EnsureEnvironment(Environments.Production);
        mode.EnsureNoOperationalCommandConflict(0);
    }

    [Fact]
    public void Exact_argument_enables_development_api_serving_mode()
    {
        var mode = ApiStartupExecutionMode.Parse([ApiStartupExecutionMode.ExactArgument]);

        mode.EnsureEnvironment(Environments.Development);
        mode.EnsureNoOperationalCommandConflict(0);
        Assert.False(mode.RunStartupMaintenance);
    }

    [Theory]
    [InlineData("--SERVE-API-WITHOUT-STARTUP-MAINTENANCE")]
    [InlineData("--serve-api-without-startup-maintenance=true")]
    public void Case_or_prefix_variant_is_rejected(string argument)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApiStartupExecutionMode.Parse([argument]));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_ARGUMENT_INVALID", exception.Message);
    }

    [Fact]
    public void Duplicate_exact_argument_is_rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ApiStartupExecutionMode.Parse(
            [ApiStartupExecutionMode.ExactArgument, ApiStartupExecutionMode.ExactArgument]));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_ARGUMENT_DUPLICATE", exception.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Non_development_environment_is_rejected(string environmentName)
    {
        var mode = ApiStartupExecutionMode.Parse([ApiStartupExecutionMode.ExactArgument]);

        var exception = Assert.Throws<InvalidOperationException>(() => mode.EnsureEnvironment(environmentName));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_ENVIRONMENT_NOT_ALLOWED", exception.Message);
    }

    [Fact]
    public void Existing_operational_command_conflict_is_rejected()
    {
        var mode = ApiStartupExecutionMode.Parse([ApiStartupExecutionMode.ExactArgument]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            mode.EnsureNoOperationalCommandConflict(1));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_OPERATIONAL_COMMAND_CONFLICT", exception.Message);
    }

    [Theory]
    [InlineData("--run-audit-intent-temporal-migration")]
    [InlineData("--Run-Audit-Intent-Temporal-Migration")]
    [InlineData("--run-unknown-operational-command")]
    public void Operational_argument_or_lookalike_is_rejected_during_initial_parse(string argument)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ApiStartupExecutionMode.Parse(
            [ApiStartupExecutionMode.ExactArgument, argument]));

        Assert.Equal("API_STARTUP_EXECUTION_MODE_OPERATIONAL_COMMAND_CONFLICT", exception.Message);
    }

    [Fact]
    public void Normal_mode_registers_module_registration_worker()
    {
        var services = new ServiceCollection();
        var mode = ApiStartupExecutionMode.Parse([]);

        mode.AddModuleRegistrationHostedService(services);

        Assert.Contains(services, IsModuleRegistrationWorker);
    }

    [Fact]
    public void Api_serving_mode_omits_module_registration_worker()
    {
        var services = new ServiceCollection();
        var mode = ApiStartupExecutionMode.Parse([ApiStartupExecutionMode.ExactArgument]);

        mode.AddModuleRegistrationHostedService(services);

        Assert.DoesNotContain(services, IsModuleRegistrationWorker);
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

    private static async Task<PipelineResult> ExecuteProtectedPipelineAsync(
        Guid tenantHeader,
        Guid? authenticatedTenantClaim,
        string? permission)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddScoped<TenantContext>();
        services
            .AddAuthentication(TestAuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, PipelineAuthenticationHandler>(
                TestAuthenticationScheme,
                _ => { });
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

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
        context.Request.Path = "/api/gskus/startup-mode-pipeline-test";
        context.Request.Headers["X-Tenant-Id"] = tenantHeader.ToString();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new HasPermissionAttribute(RequiredPermission)),
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

    private static bool IsModuleRegistrationWorker(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationType == typeof(ModuleRegistrationHostedService);

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
}

public sealed class ApiStartupExecutionModeMongoTests(AuditIntentTemporalMongoFixture mongo)
    : IClassFixture<AuditIntentTemporalMongoFixture>
{
    [Fact]
    public async Task Api_serving_mode_does_not_run_legal_entity_startup_migration()
    {
        var databaseName = $"diten_mdm_api_startup_mode_{Guid.NewGuid():N}";
        var client = new MongoClient(mongo.StandaloneConnectionString);
        var database = client.GetDatabase(databaseName);
        var collection = database.GetCollection<BsonDocument>("mdm_legal_entities");
        var id = Guid.NewGuid();
        await collection.InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
            ["TenantId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["LifecycleStatus"] = 2
        });

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mongo:ConnectionString"] = mongo.StandaloneConnectionString,
                    ["Mongo:DatabaseName"] = databaseName
                })
                .Build();
            var services = new ServiceCollection();

            Diten.MdmService.Persistence.DependencyInjection.AddPersistence(
                services,
                configuration,
                runStartupMaintenance: false);

            var persisted = await collection.Find(Builders<BsonDocument>.Filter.Eq(
                "_id",
                new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync();
            Assert.Equal(2, persisted["LifecycleStatus"].AsInt32);
            Assert.False(persisted.Contains("OperationalStatus"));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }
}
