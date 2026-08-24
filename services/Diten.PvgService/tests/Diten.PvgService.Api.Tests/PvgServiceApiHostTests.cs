using Diten.PvgService.Api;
using Diten.PvgService.Application.CaseProcessing;
using Diten.PvgService.Application.MeddraCoding;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Application.SignalManagement;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    public void Case_intake_route_metadata_allows_only_approved_methods_per_template()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddPvgServiceApiHost(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.MapPvgCaseIntakeTriageEndpoints();

        var routeMethods = RouteMethodMap(app);

        Assert.Equal(
            new[]
            {
                PvgCaseIntakeTriageEndpoints.RoutePrefix,
                $"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/{{intakeDraftId}}",
                $"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/{{intakeDraftId}}/route",
                $"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/{{intakeDraftId}}/triage"
            },
            routeMethods.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(["GET", "POST"], routeMethods[PvgCaseIntakeTriageEndpoints.RoutePrefix].Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["GET", "PUT"], routeMethods[$"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/{{intakeDraftId}}"].Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["POST"], routeMethods[$"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/{{intakeDraftId}}/route"].Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["POST"], routeMethods[$"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/{{intakeDraftId}}/triage"].Order(StringComparer.Ordinal).ToArray());

        foreach (var method in routeMethods.Values.SelectMany(methods => methods))
        {
            Assert.Contains(method, new[] { "GET", "POST", "PUT" });
            Assert.NotEqual("DELETE", method);
            Assert.NotEqual("PATCH", method);
            Assert.NotEqual("OPTIONS", method);
        }
    }

    [Fact]
    public void Case_intake_endpoint_methods_and_public_dtos_do_not_expose_retention_or_forbidden_operations()
    {
        var apiSurfaceNames = new[]
        {
            typeof(PvgCaseIntakeTriageEndpoints),
            typeof(PvgCaseIntakeCreateRequest),
            typeof(PvgCaseIntakeUpdateRequest),
            typeof(PvgCaseIntakeTriageRequest),
            typeof(PvgCaseIntakeRouteRequest),
            typeof(PvgCaseIntakeApiResponse),
            typeof(PvgCaseIntakeRequestContext),
            typeof(PvgIntakeDraftApplicationService)
        }
        .SelectMany(type => PublicMemberNames(type).Append(type.Name))
        .ToArray();

        AssertNoForbiddenRuntimeSurface(apiSurfaceNames);
    }

    [Fact]
    public void Case_intake_triage_route_templates_and_endpoint_names_do_not_expose_forbidden_surfaces()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddPvgServiceApiHost(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.MapPvgServiceHealthEndpoints();
        app.MapPvgCaseIntakeTriageEndpoints();

        var endpointSurfaces = EndpointSurfaces(app);
        var forbiddenTerms = new[]
        {
            "delete",
            "bulk-delete",
            "bulk",
            "archive",
            "void",
            "export",
            "ai",
            "meddra",
            "dictionary",
            "import",
            "search",
            "mod-0231",
            "mod-0232",
            "mod-0234",
            "case-processing",
            "caseprocessing",
            "meddra-coding",
            "meddracoding",
            "signal-management",
            "signalmanagement"
        };

        foreach (var endpointSurface in endpointSurfaces)
        {
            foreach (var forbiddenTerm in forbiddenTerms)
            {
                Assert.False(
                    ContainsForbiddenSurface(endpointSurface, forbiddenTerm),
                    $"Endpoint surface '{endpointSurface}' must not expose '{forbiddenTerm}'.");
            }
        }
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
            var propertyNames = requestType.GetProperties().Select(property => property.Name).ToArray();
            var forbiddenClientTenantFields = new[]
            {
                "TenantId",
                "Tenant",
                "TenantReference",
                "TenantContext",
                "ClientTenant",
                "ClientTenantId",
                "ClientTenantReference",
                "ClientTenantContext"
            };

            foreach (var propertyName in propertyNames)
            {
                foreach (var forbiddenField in forbiddenClientTenantFields)
                {
                    Assert.False(
                        propertyName.Contains(forbiddenField, StringComparison.OrdinalIgnoreCase),
                        $"{requestType.Name}.{propertyName} must not expose client tenant field '{forbiddenField}'.");
                }
            }
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

    [Fact]
    public async Task Case_intake_business_endpoints_return_consistent_reason_codes_before_service_when_context_is_missing()
    {
        var createRequest = new PvgCaseIntakeCreateRequest(
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
        var endpoints = new (string Name, Func<DefaultHttpContext, ValueTask<IResult>> Invoke)[]
        {
            ("create", context => PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
                createRequest,
                context,
                service: null!,
                CancellationToken.None)),
            ("update", context => PvgCaseIntakeTriageEndpoints.UpdateDraftAsync(
                "draft-reference",
                EmptyUpdateRequest(),
                context,
                service: null!,
                CancellationToken.None)),
            ("list", context => PvgCaseIntakeTriageEndpoints.ListDraftsAsync(
                context,
                service: null!,
                cancellationToken: CancellationToken.None)),
            ("detail", context => PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
                "draft-reference",
                context,
                service: null!,
                CancellationToken.None)),
            ("triage", context => PvgCaseIntakeTriageEndpoints.TriageDraftAsync(
                "draft-reference",
                new PvgCaseIntakeTriageRequest(null, null, null),
                context,
                service: null!,
                CancellationToken.None)),
            ("route", context => PvgCaseIntakeTriageEndpoints.RouteDraftAsync(
                "draft-reference",
                new PvgCaseIntakeRouteRequest(null),
                context,
                service: null!,
                CancellationToken.None))
        };

        foreach (var endpoint in endpoints)
        {
            var missingActor = await ResponseOfAsync(await endpoint.Invoke(NewContextWithTenant()));
            AssertSafeBlockedResponse(missingActor, PvgPermissionReasonCodes.ActorContextRequired);

            var missingCorrelationContext = NewContextWithTenant();
            missingCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
            missingCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";
            var missingCorrelation = await ResponseOfAsync(await endpoint.Invoke(missingCorrelationContext));
            AssertSafeBlockedResponse(missingCorrelation, PvgPermissionReasonCodes.CorrelationContextRequired);
        }
    }

    [Fact]
    public async Task Case_intake_business_endpoints_return_safe_tenant_reason_code_before_service_when_tenant_is_missing()
    {
        var createRequest = new PvgCaseIntakeCreateRequest(
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
        var endpoints = new (string Name, Func<DefaultHttpContext, ValueTask<IResult>> Invoke)[]
        {
            ("create", context => PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
                createRequest,
                context,
                service: null!,
                CancellationToken.None)),
            ("update", context => PvgCaseIntakeTriageEndpoints.UpdateDraftAsync(
                "draft-reference",
                EmptyUpdateRequest(),
                context,
                service: null!,
                CancellationToken.None)),
            ("list", context => PvgCaseIntakeTriageEndpoints.ListDraftsAsync(
                context,
                service: null!,
                cancellationToken: CancellationToken.None)),
            ("detail", context => PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
                "draft-reference",
                context,
                service: null!,
                CancellationToken.None)),
            ("triage", context => PvgCaseIntakeTriageEndpoints.TriageDraftAsync(
                "draft-reference",
                new PvgCaseIntakeTriageRequest(null, null, null),
                context,
                service: null!,
                CancellationToken.None)),
            ("route", context => PvgCaseIntakeTriageEndpoints.RouteDraftAsync(
                "draft-reference",
                new PvgCaseIntakeRouteRequest(null),
                context,
                service: null!,
                CancellationToken.None))
        };

        foreach (var endpoint in endpoints)
        {
            var missingTenant = await ResponseOfAsync(await endpoint.Invoke(new DefaultHttpContext()));
            AssertSafeBlockedResponse(missingTenant, PvgValidationReasonCodes.TenantContextRequired);
        }
    }

    [Fact]
    public void Case_intake_api_response_contract_exposes_safe_observability_error_model_only()
    {
        var properties = typeof(PvgCaseIntakeApiResponse)
            .GetProperties()
            .Select(property => (property.Name, property.PropertyType))
            .ToArray();

        Assert.Equal(
            [
                (nameof(PvgCaseIntakeApiResponse.Outcome), typeof(string)),
                (nameof(PvgCaseIntakeApiResponse.ReasonCode), typeof(string)),
                (nameof(PvgCaseIntakeApiResponse.ValidationReasonCodes), typeof(IReadOnlyList<string>)),
                (nameof(PvgCaseIntakeApiResponse.IntakeDraftId), typeof(string)),
                (nameof(PvgCaseIntakeApiResponse.Items), typeof(IReadOnlyList<PvgIntakeDraftSummary>))
            ],
            properties);

        Assert.DoesNotContain(properties, property =>
            property.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Actor", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Correlation", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Trace", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Audit", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Metadata", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Reporter", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Patient", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Product", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Evidence", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Queue", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Phi", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Pii", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Case_intake_blocked_observability_responses_do_not_echo_trace_headers_or_request_values()
    {
        var endpoints = new (string Name, Func<DefaultHttpContext, ValueTask<IResult>> Invoke)[]
        {
            ("create", context => PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
                SensitiveCreateRequest(),
                context,
                service: null!,
                CancellationToken.None)),
            ("update", context => PvgCaseIntakeTriageEndpoints.UpdateDraftAsync(
                "draft-reference",
                SensitiveUpdateRequest(),
                context,
                service: null!,
                CancellationToken.None)),
            ("list", context => PvgCaseIntakeTriageEndpoints.ListDraftsAsync(
                context,
                service: null!,
                cancellationToken: CancellationToken.None)),
            ("detail", context => PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
                "draft-reference",
                context,
                service: null!,
                CancellationToken.None)),
            ("triage", context => PvgCaseIntakeTriageEndpoints.TriageDraftAsync(
                "draft-reference",
                new PvgCaseIntakeTriageRequest(PvgTriageOutcome.Rejected, "PVG_TRIAGE_REASON_REJECTED", "triage free-text reason"),
                context,
                service: null!,
                CancellationToken.None)),
            ("route", context => PvgCaseIntakeTriageEndpoints.RouteDraftAsync(
                "draft-reference",
                new PvgCaseIntakeRouteRequest("queue-safety-review"),
                context,
                service: null!,
                CancellationToken.None))
        };

        foreach (var endpoint in endpoints)
        {
            var invalidCorrelationContext = NewContextWithTenant();
            invalidCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-reference";
            invalidCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "safety-user";
            invalidCorrelationContext.Request.Headers[PvgCaseIntakeRequestContext.CorrelationIdHeader] = "unsafe correlation value";

            var response = await ResponseOfAsync(await endpoint.Invoke(invalidCorrelationContext));

            AssertSafeBlockedResponse(response, PvgPermissionReasonCodes.CorrelationContextInvalid);
        }
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
    public async Task Case_intake_reserved_operation_paths_return_not_found_without_error_payload_echo(string reservedWord)
    {
        var updateResult = await PvgCaseIntakeTriageEndpoints.UpdateDraftAsync(
            reservedWord,
            SensitiveUpdateRequest(),
            NewContextWithTenantActorAndCorrelation(),
            service: null!,
            CancellationToken.None);
        var triageResult = await PvgCaseIntakeTriageEndpoints.TriageDraftAsync(
            reservedWord,
            new PvgCaseIntakeTriageRequest(PvgTriageOutcome.Rejected, "PVG_TRIAGE_REASON_REJECTED", "triage free-text reason"),
            NewContextWithTenantActorAndCorrelation(),
            service: null!,
            CancellationToken.None);
        var routeResult = await PvgCaseIntakeTriageEndpoints.RouteDraftAsync(
            reservedWord,
            new PvgCaseIntakeRouteRequest("queue-safety-review"),
            NewContextWithTenantActorAndCorrelation(),
            service: null!,
            CancellationToken.None);

        foreach (var result in new[] { updateResult, triageResult, routeResult })
        {
            var response = await RawResponseOfAsync(result);

            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
            Assert.DoesNotContain(reservedWord, response.Body, StringComparison.OrdinalIgnoreCase);
            AssertNoUnsafeResponseSamples(response.Body);
        }
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

    private static Dictionary<string, string[]> RouteMethodMap(WebApplication app) =>
        ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .GroupBy(
                endpoint => endpoint.RoutePattern.RawText ?? string.Empty,
                endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [],
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(methods => methods).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

    private static string[] EndpointSurfaces(WebApplication app) =>
        ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => new[]
            {
                endpoint.RoutePattern.RawText ?? string.Empty,
                endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? string.Empty,
                endpoint.DisplayName ?? string.Empty
            })
            .Where(surface => !string.IsNullOrWhiteSpace(surface))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool ContainsForbiddenSurface(string surface, string forbiddenTerm) =>
        string.Equals(forbiddenTerm, "ai", StringComparison.OrdinalIgnoreCase)
            ? Regex.IsMatch(surface, @"(^|[^A-Za-z])ai([^A-Za-z]|$)", RegexOptions.IgnoreCase)
            : surface.Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase);

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

    private static PvgCaseIntakeCreateRequest SensitiveCreateRequest() =>
        new(
            "channel",
            "source",
            "source-ref",
            DateTimeOffset.UtcNow,
            "reporter",
            "reporter@example.test",
            "patient-subject-code",
            DateOnly.FromDateTime(DateTime.UnixEpoch),
            "free text narrative with PHI",
            "suspect product",
            "serious",
            "priority",
            ["evidence-ref"]);

    private static PvgCaseIntakeUpdateRequest SensitiveUpdateRequest() =>
        new(
            "channel",
            "source",
            "source-ref",
            DateTimeOffset.UtcNow,
            "reporter",
            "reporter@example.test",
            "patient-subject-code",
            DateOnly.FromDateTime(DateTime.UnixEpoch),
            "free text narrative with PHI",
            "suspect product",
            "serious",
            "priority",
            ["evidence-ref"]);

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

    private static async Task<(int StatusCode, string Body)> RawResponseOfAsync(IResult result)
    {
        var responseContext = new DefaultHttpContext();
        responseContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        responseContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(responseContext);
        responseContext.Response.Body.Position = 0;

        using var reader = new StreamReader(responseContext.Response.Body);
        return (responseContext.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static async Task<(int StatusCode, PvgCaseIntakeApiResponse Body)> ResponseOfAsync(IResult result)
    {
        var responseContext = new DefaultHttpContext();
        responseContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        responseContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(responseContext);
        responseContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<PvgCaseIntakeApiResponse>(
            responseContext.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(body);
        return (responseContext.Response.StatusCode, body);
    }

    private static void AssertSafeBlockedResponse(
        (int StatusCode, PvgCaseIntakeApiResponse Body) response,
        string expectedReasonCode)
    {
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Equal(nameof(PvgApplicationOutcome.Blocked), response.Body.Outcome);
        Assert.Equal(expectedReasonCode, response.Body.ReasonCode);
        Assert.Matches("^PVG_[A-Z0-9_]+$", response.Body.ReasonCode);
        Assert.Empty(response.Body.ValidationReasonCodes);
        Assert.Null(response.Body.IntakeDraftId);
        Assert.Empty(response.Body.Items);

        var serialized = JsonSerializer.Serialize(response.Body);
        AssertNoUnsafeResponseSamples(serialized);
    }

    private static void AssertNoUnsafeResponseSamples(string body)
    {
        foreach (var unsafeSample in UnsafeResponseSamples)
        {
            Assert.DoesNotContain(unsafeSample, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoForbiddenRuntimeSurface(IEnumerable<string> names)
    {
        foreach (var forbiddenTerm in ForbiddenRuntimeSurfaceTerms)
        {
            Assert.DoesNotContain(names, name => name.Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> PublicMemberNames(Type type) =>
        type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Field)
            .Select(member => member.Name);

    private static readonly string[] ForbiddenRuntimeSurfaceTerms =
    [
        "retention",
        "legalhold",
        "archive",
        "void",
        "export",
        "delete",
        "bulk-delete",
        "bulk"
    ];

    private static readonly string[] UnsafeResponseSamples =
    [
        "server-tenant-context",
        "draft-reference",
        "actor-reference",
        "safety-user",
        "correlation-reference",
        "unsafe correlation value",
        "channel",
        "source",
        "source-ref",
        "evidence-ref",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "triage free-text reason",
        "route free-text reason",
        "queue-safety-review",
        "suspect product",
        "reporter",
        "narrative",
        "serious",
        "priority"
    ];

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Diten.PvgService.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
