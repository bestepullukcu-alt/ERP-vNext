using Diten.PvgService.Api;
using Diten.PvgService.Application.CaseProcessing;
using Diten.PvgService.Application.MeddraCoding;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Application.SignalManagement;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.PvgService.Api.Tests;

public sealed class PvgServiceApiHostTests
{
    [Fact]
    public void Development_host_wires_health_application_services_and_deny_adapters()
    {
        var services = new ServiceCollection();

        services.AddPvgServiceApiHost(EmptyConfiguration(), new TestHostEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<DenyAllFieldSecurityPolicy>(provider.GetRequiredService<IPvgFieldSecurityPolicy>());
        Assert.IsType<DenyAllWorkflowTransitionGate>(provider.GetRequiredService<IPvgWorkflowTransitionGate>());
        Assert.IsType<DenyAllEvidenceLinkPort>(provider.GetRequiredService<IPvgEvidenceLinkPort>());
        Assert.IsType<DenyAllPermissionGate>(provider.GetRequiredService<IPvgPermissionGate>());
        Assert.IsType<InMemoryPvgIntakeDraftRepository>(provider.GetRequiredService<IPvgIntakeDraftStore>());
        Assert.NotNull(provider.GetRequiredService<PvgIntakeDraftApplicationService>());
        Assert.NotNull(provider.GetRequiredService<PvgCaseProcessingApplicationService>());
        Assert.NotNull(provider.GetRequiredService<InMemoryMeddraCodingApplicationService>());
        Assert.NotNull(provider.GetRequiredService<InMemorySignalManagementService>());
    }

    [Fact]
    public void Health_endpoints_are_defined_for_local_dev_ci_host()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddPvgServiceApiHost(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.MapPvgServiceHealthEndpoints();

        var routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Contains("/health/live", routes);
        Assert.Contains("/health/ready", routes);
        Assert.Equal(2, routes.Length);
    }

    [Fact]
    public void Case_intake_triage_endpoints_define_only_approved_local_dev_ci_routes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddPvgServiceApiHost(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.MapPvgServiceHealthEndpoints();
        app.MapPvgCaseIntakeTriageEndpoints();

        var routes = RouteMethods(app);

        Assert.Equal(
            new[]
            {
                "GET /api/v1/pv-case-intake-triage",
                "GET /api/v1/pv-case-intake-triage/{intakeDraftId}",
                "GET /health/live",
                "GET /health/ready",
                "POST /api/v1/pv-case-intake-triage",
                "POST /api/v1/pv-case-intake-triage/{intakeDraftId}/route",
                "POST /api/v1/pv-case-intake-triage/{intakeDraftId}/triage",
                "PUT /api/v1/pv-case-intake-triage/{intakeDraftId}"
            },
            routes);

        Assert.DoesNotContain(routes, route => route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.DoesNotContain(routes, route => route.Contains("archive", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, route => route.Contains("void", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, route => route.Contains("export", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, route => route.Contains("bulk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Public_case_intake_request_shapes_do_not_accept_tenant_id()
    {
        var requestTypes = new[]
        {
            typeof(PvgCaseIntakeCreateRequest),
            typeof(PvgCaseIntakeUpdateRequest),
            typeof(PvgCaseIntakeTriageRequest),
            typeof(PvgCaseIntakeRouteRequest)
        };

        foreach (var requestType in requestTypes)
        {
            Assert.DoesNotContain(
                requestType.GetProperties(),
                property => property.Name.Contains("TenantId", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Case_intake_request_context_fails_closed_when_tenant_actor_or_correlation_is_missing()
    {
        var missingTenant = PvgCaseIntakeRequestContext.From(new DefaultHttpContext());
        Assert.False(missingTenant.IsValid);
        Assert.Equal(PvgValidationReasonCodes.TenantContextRequired, missingTenant.ReasonCode);

        var missingActor = NewContextWithTenant();
        var missingActorContext = PvgCaseIntakeRequestContext.From(missingActor);
        Assert.False(missingActorContext.IsValid);
        Assert.Equal(PvgPermissionReasonCodes.ActorContextRequired, missingActorContext.ReasonCode);

        var missingCorrelation = NewContextWithTenant();
        missingCorrelation.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
        missingCorrelation.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";
        var missingCorrelationContext = PvgCaseIntakeRequestContext.From(missingCorrelation);
        Assert.False(missingCorrelationContext.IsValid);
        Assert.Equal(PvgPermissionReasonCodes.CorrelationContextRequired, missingCorrelationContext.ReasonCode);

        var invalidCorrelation = NewContextWithTenant();
        invalidCorrelation.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
        invalidCorrelation.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";
        invalidCorrelation.Request.Headers[PvgCaseIntakeRequestContext.CorrelationIdHeader] = "unsafe correlation";
        var invalidCorrelationContext = PvgCaseIntakeRequestContext.From(invalidCorrelation);
        Assert.False(invalidCorrelationContext.IsValid);
        Assert.Equal(PvgPermissionReasonCodes.CorrelationContextInvalid, invalidCorrelationContext.ReasonCode);
    }

    [Fact]
    public async Task Case_intake_create_endpoint_fails_closed_before_service_when_context_is_missing()
    {
        var request = new PvgCaseIntakeCreateRequest(
            "channel",
            "source",
            null,
            DateTimeOffset.UtcNow,
            "reporter",
            null,
            null,
            null,
            "narrative",
            null,
            "serious",
            "priority",
            null);

        var missingTenantResult = await PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
            request,
            new DefaultHttpContext(),
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingTenantResult));

        var missingActorContext = NewContextWithTenant();
        var missingActorResult = await PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
            request,
            missingActorContext,
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingActorResult));

        var missingCorrelationContext = NewContextWithTenant();
        missingCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
        missingCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";
        var missingCorrelationResult = await PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
            request,
            missingCorrelationContext,
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingCorrelationResult));
    }

    [Fact]
    public async Task Case_intake_read_endpoints_fail_closed_before_service_when_actor_or_correlation_is_missing()
    {
        var missingActorListResult = await PvgCaseIntakeTriageEndpoints.ListDraftsAsync(
            NewContextWithTenant(),
            service: null!,
            cancellationToken: CancellationToken.None);

        var missingActorDetailResult = await PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
            "draft-reference",
            NewContextWithTenant(),
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingActorListResult));
        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingActorDetailResult));

        var missingCorrelationContext = NewContextWithTenant();
        missingCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
        missingCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";

        var missingCorrelationListResult = await PvgCaseIntakeTriageEndpoints.ListDraftsAsync(
            missingCorrelationContext,
            service: null!,
            cancellationToken: CancellationToken.None);

        var missingCorrelationDetailResult = await PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
            "draft-reference",
            missingCorrelationContext,
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingCorrelationListResult));
        Assert.Equal(StatusCodes.Status409Conflict, await StatusCodeOfAsync(missingCorrelationDetailResult));
    }

    [Theory]
    [InlineData("export")]
    [InlineData("archive")]
    [InlineData("void")]
    [InlineData("bulk")]
    [InlineData("bulk-delete")]
    [InlineData("delete")]
    public async Task Case_intake_reserved_operation_words_are_not_accepted_as_detail_or_update_ids(string reservedWord)
    {
        var getResult = await PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
            reservedWord,
            NewContextWithTenant(),
            service: null!,
            CancellationToken.None);

        var updateResult = await PvgCaseIntakeTriageEndpoints.UpdateDraftAsync(
            reservedWord,
            EmptyUpdateRequest(),
            NewContextWithTenantActorAndCorrelation(),
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, await StatusCodeOfAsync(getResult));
        Assert.Equal(StatusCodes.Status404NotFound, await StatusCodeOfAsync(updateResult));
    }

    [Theory]
    [InlineData("export")]
    [InlineData("archive")]
    [InlineData("void")]
    [InlineData("bulk")]
    [InlineData("bulk-delete")]
    [InlineData("delete")]
    public async Task Case_intake_reserved_operation_words_are_not_accepted_as_triage_or_route_ids(string reservedWord)
    {
        var triageResult = await PvgCaseIntakeTriageEndpoints.TriageDraftAsync(
            reservedWord,
            new PvgCaseIntakeTriageRequest(null, null, null),
            NewContextWithTenantActorAndCorrelation(),
            service: null!,
            CancellationToken.None);

        var routeResult = await PvgCaseIntakeTriageEndpoints.RouteDraftAsync(
            reservedWord,
            new PvgCaseIntakeRouteRequest(null),
            NewContextWithTenantActorAndCorrelation(),
            service: null!,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, await StatusCodeOfAsync(triageResult));
        Assert.Equal(StatusCodes.Status404NotFound, await StatusCodeOfAsync(routeResult));
    }

    [Fact]
    public void Production_like_startup_refuses_without_operational_runtime_authorization()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPvgServiceApiHost(EmptyConfiguration(), new TestHostEnvironment(Environments.Production)));

        Assert.Contains("operational runtime is not authorized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_like_startup_refuses_non_production_adapters_even_when_runtime_authorized()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true",
                ["Pvg:Runtime:UseNonProductionAdapters"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPvgServiceApiHost(configuration, new TestHostEnvironment(Environments.Production)));

        Assert.Contains("cannot use non-production adapters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_like_authorized_startup_does_not_register_non_production_adapters()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true"
            })
            .Build();

        services.AddPvgServiceApiHost(configuration, new TestHostEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(provider.GetService<IPvgFieldSecurityPolicy>());
        Assert.Null(provider.GetService<IPvgWorkflowTransitionGate>());
        Assert.Null(provider.GetService<IPvgEvidenceLinkPort>());
        Assert.Null(provider.GetService<IPvgPermissionGate>());
        Assert.Null(provider.GetService<IPvgIntakeDraftStore>());
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    private static string[] RouteMethods(WebApplication app) =>
        ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
                return methods.Select(method => $"{method} {endpoint.RoutePattern.RawText}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static DefaultHttpContext NewContextWithTenant()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[PvgCaseIntakeRequestContext.TenantContextHeader] = "server-tenant-context";
        return context;
    }

    private static DefaultHttpContext NewContextWithTenantActorAndCorrelation()
    {
        var context = NewContextWithTenant();
        context.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
        context.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";
        context.Request.Headers[PvgCaseIntakeRequestContext.CorrelationIdHeader] = "correlation-reference";
        return context;
    }

    private static PvgCaseIntakeUpdateRequest EmptyUpdateRequest() =>
        new(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static async Task<int> StatusCodeOfAsync(IResult result)
    {
        var responseContext = new DefaultHttpContext();
        responseContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        responseContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(responseContext);
        return responseContext.Response.StatusCode;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Diten.PvgService.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
