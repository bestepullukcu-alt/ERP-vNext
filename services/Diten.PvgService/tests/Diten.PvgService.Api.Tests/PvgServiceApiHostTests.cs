using Diten.PvgService.Api;
using Diten.PvgService.Application.CaseProcessing;
using Diten.PvgService.Application.MeddraCoding;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Application.SignalManagement;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.PvgService.Api.Tests;

public sealed class PvgServiceApiHostTests
{
    // Revision 04 invariant (WP-0230-DL4A): every approved intake field name, plus the version a write carries and the
    // two response-only fields a request may only send to be rejected. SuspectProductText no longer exists.
    private static readonly string[] AnnexFieldNames =
        Enum.GetNames<PvgIntakeField>()
            .Concat(
            [
                "ExpectedVersion",
                "ValidityResult",
                "ValidityMissingCriteria"
            ])
            .ToArray();

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
    public void Public_case_intake_request_shapes_cover_annex_fields_without_extra_user_payload_fields()
    {
        var requestFieldNames = new[]
        {
            typeof(PvgCaseIntakeCreateRequest),
            typeof(PvgCaseIntakeUpdateRequest),
            typeof(PvgCaseIntakeTriageRequest),
            typeof(PvgCaseIntakeRouteRequest),
            typeof(PvgIntakeProductLineRequest)
        }
        .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        .Select(property => property.Name)
        .Where(name => !string.Equals(name, nameof(PvgCaseIntakeTriageRequest.TriageReasonCode), StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

        Assert.Equal(37, AnnexFieldNames.Length);
        Assert.DoesNotContain("SuspectProductText", requestFieldNames);
        Assert.Equal(AnnexFieldNames.Order(StringComparer.Ordinal).ToArray(), requestFieldNames);
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
                (nameof(PvgCaseIntakeApiResponse.Items), typeof(IReadOnlyList<PvgIntakeDraftSummary>)),
                (nameof(PvgCaseIntakeApiResponse.Version), typeof(long?)),
                (nameof(PvgCaseIntakeApiResponse.TotalCount), typeof(int?)),
                (nameof(PvgCaseIntakeApiResponse.FilteredCount), typeof(int?)),
                (nameof(PvgCaseIntakeApiResponse.Draft), typeof(PvgCaseIntakeDraftResponse))
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

    [Theory]
    [InlineData(typeof(IPvgFieldSecurityPolicy), "PVG_DEPENDENCY_FIELD_SECURITY")]
    [InlineData(typeof(IPvgWorkflowTransitionGate), "PVG_DEPENDENCY_WORKFLOW_GATE")]
    [InlineData(typeof(IPvgEvidenceLinkPort), "PVG_DEPENDENCY_EVIDENCE_LINK")]
    [InlineData(typeof(IPvgPermissionGate), "PVG_DEPENDENCY_PERMISSION_GATE")]
    [InlineData(typeof(IPvgIntakeDraftStore), "PVG_DEPENDENCY_INTAKE_STORE")]
    public void Production_like_startup_refuses_when_a_required_dependency_is_missing(
        Type missingDependency,
        string expectedCode)
    {
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            Environments.Production,
            AuthorizedRuntimeConfiguration(),
            SetupMethodUrls,
            services => RegisterTestDependenciesExcept(services, missingDependency));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(expectedCode, exception.Message, StringComparison.Ordinal);
        foreach (var otherCode in RequiredDependencyCodes.Where(code => code != expectedCode))
        {
            Assert.DoesNotContain(otherCode, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Production_like_authorized_host_build_refuses_without_required_dependencies()
    {
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            environmentName: null,
            AuthorizedRuntimeConfiguration(),
            SetupMethodUrls);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        foreach (var code in RequiredDependencyCodes)
        {
            Assert.Contains(code, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Production_like_startup_refusal_message_carries_only_safe_dependency_codes()
    {
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            Environments.Production,
            AuthorizedRuntimeConfiguration(),
            SetupMethodUrls);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal(
            RequiredDependencyRefusalText + string.Join(", ", RequiredDependencyCodes),
            exception.Message);
        Assert.Equal(
            RequiredDependencyCodes,
            Regex.Matches(exception.Message, "PVG_[A-Z0-9_]+").Select(match => match.Value).ToArray());
        AssertNoTypeNames(exception.Message);
        Assert.DoesNotContain("Exception", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(IPvgFieldSecurityPolicy), "PVG_DEPENDENCY_FIELD_SECURITY", "Development")]
    [InlineData(typeof(IPvgWorkflowTransitionGate), "PVG_DEPENDENCY_WORKFLOW_GATE", "Development")]
    [InlineData(typeof(IPvgEvidenceLinkPort), "PVG_DEPENDENCY_EVIDENCE_LINK", "Development")]
    [InlineData(typeof(IPvgPermissionGate), "PVG_DEPENDENCY_PERMISSION_GATE", "Development")]
    [InlineData(typeof(IPvgIntakeDraftStore), "PVG_DEPENDENCY_INTAKE_STORE", "Development")]
    [InlineData(typeof(IPvgFieldSecurityPolicy), "PVG_DEPENDENCY_FIELD_SECURITY", "Staging")]
    [InlineData(typeof(IPvgWorkflowTransitionGate), "PVG_DEPENDENCY_WORKFLOW_GATE", "Staging")]
    [InlineData(typeof(IPvgEvidenceLinkPort), "PVG_DEPENDENCY_EVIDENCE_LINK", "Staging")]
    [InlineData(typeof(IPvgPermissionGate), "PVG_DEPENDENCY_PERMISSION_GATE", "Staging")]
    [InlineData(typeof(IPvgIntakeDraftStore), "PVG_DEPENDENCY_INTAKE_STORE", "Staging")]
    public async Task Health_ready_returns_503_with_safe_codes_when_a_required_dependency_is_missing(
        Type missingDependency,
        string expectedCode,
        string environmentName)
    {
        using var host = BuildConfiguredSetupMethodHost(
            environmentName,
            services => services.RemoveAll(missingDependency));

        var response = await InvokeMappedHealthReadyAsync(host);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);

        using var document = JsonDocument.Parse(response.Body);
        var fieldNames = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(NotReadyBodyFieldNames.Order(StringComparer.Ordinal).ToArray(), fieldNames);
        Assert.Equal("not-ready", document.RootElement.GetProperty("status").GetString());

        var codes = document.RootElement
            .GetProperty("missingDependencyCodes")
            .EnumerateArray()
            .Select(code => code.GetString())
            .ToArray();
        Assert.Equal(new string?[] { expectedCode }, codes);
        Assert.All(codes, code => Assert.Matches("^PVG_[A-Z0-9_]+$", code));
        AssertNoTypeNames(response.Body);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task Health_ready_returns_200_when_all_required_dependencies_resolve(string environmentName)
    {
        using var host = BuildConfiguredSetupMethodHost(environmentName);

        var response = await InvokeMappedHealthReadyAsync(host);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal("ready", document.RootElement.GetProperty("status").GetString());
        if (document.RootElement.TryGetProperty("missingDependencyCodes", out var codes))
        {
            Assert.Empty(codes.EnumerateArray());
        }
    }

    [Fact]
    public void Setup_method_host_maps_health_and_case_intake_routes()
    {
        using var host = BuildConfiguredSetupMethodHost(Environments.Development);

        var routes = MappedEndpoints(host)
            .SelectMany(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
                return methods.Select(method => $"{method} {endpoint.RoutePattern.RawText}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

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
    public void Program_create_web_host_builder_refuses_production_like_build_without_required_dependencies()
    {
        var builder = Program.CreateWebHostBuilder(
        [
            "--Pvg:Runtime:ProductionLike=true",
            "--Pvg:Runtime:OperationalRuntimeAuthorized=true",
            "--Pvg:Runtime:UseNonProductionAdapters=false"
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        foreach (var code in RequiredDependencyCodes)
        {
            Assert.Contains(code, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Program_top_level_statements_only_call_create_web_host_builder()
    {
        var programPath = ProgramSourcePath();
        Assert.True(File.Exists(programPath), $"Program source not found at '{programPath}'.");

        var code = StripCommentsAndStringLiterals(File.ReadAllText(programPath));

        Assert.Equal(1, CountMatches(code, @"\bCreateWebHostBuilder\s*\(\s*args\s*\)\s*\.\s*Build\s*\(\s*\)\s*\.\s*Run\s*\(\s*\)"));
        Assert.Equal(1, CountMatches(code, $@"\b{nameof(PvgServiceApiHost.CreatePvgServiceWebHostBuilder)}\s*\("));

        var forbiddenPatterns = new[]
        {
            @"\bnew\s+WebHostBuilder\b",
            @"\bWebHost\s*\.\s*CreateDefaultBuilder\b",
            @"\bWebApplication\s*\.\s*CreateBuilder\b",
            @"\bWebApplication\s*\.\s*CreateSlimBuilder\b",
            @"\bWebApplication\s*\.\s*CreateEmptyBuilder\b",
            @"\bHost\s*\.\s*CreateDefaultBuilder\b",
            @"\bHost\s*\.\s*CreateApplicationBuilder\b",
            @"\bnew\s+HostBuilder\b",
            @"\bAddPvgServiceApiHost\b",
            @"\bConfigureServices\b"
        };

        foreach (var forbiddenPattern in forbiddenPatterns)
        {
            Assert.True(
                CountMatches(code, forbiddenPattern) == 0,
                $"Program.cs must not contain '{forbiddenPattern}'.");
        }
    }

    private const string SetupMethodUrls = "http://127.0.0.1:5011";

    private const string RequiredDependencyRefusalText =
        "PVG production-like startup refused; required dependencies are not registered: ";

    private static readonly string[] RequiredDependencyCodes =
    [
        "PVG_DEPENDENCY_FIELD_SECURITY",
        "PVG_DEPENDENCY_WORKFLOW_GATE",
        "PVG_DEPENDENCY_EVIDENCE_LINK",
        "PVG_DEPENDENCY_PERMISSION_GATE",
        "PVG_DEPENDENCY_INTAKE_STORE"
    ];

    private static readonly string[] NotReadyBodyFieldNames =
    [
        "status",
        "mode",
        "operationalRuntimeAuthorized",
        "nonProductionAdaptersEnabled",
        "missingDependencyCodes"
    ];

    private static readonly (Type ServiceType, Action<IServiceCollection> Register)[] TestDependencyRegistrations =
    [
        (typeof(IPvgFieldSecurityPolicy), services => services.AddSingleton<IPvgFieldSecurityPolicy, TestFieldSecurityPolicy>()),
        (typeof(IPvgWorkflowTransitionGate), services => services.AddSingleton<IPvgWorkflowTransitionGate, TestWorkflowTransitionGate>()),
        (typeof(IPvgEvidenceLinkPort), services => services.AddSingleton<IPvgEvidenceLinkPort, TestEvidenceLinkPort>()),
        (typeof(IPvgPermissionGate), services => services.AddSingleton<IPvgPermissionGate, TestPermissionGate>()),
        (typeof(IPvgIntakeDraftStore), services => services.AddSingleton<IPvgIntakeDraftStore, TestIntakeDraftStore>())
    ];

    private static IConfiguration AuthorizedRuntimeConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true"
            })
            .Build();

    private static void RegisterTestDependenciesExcept(IServiceCollection services, Type missingDependency)
    {
        foreach (var registration in TestDependencyRegistrations.Where(registration => registration.ServiceType != missingDependency))
        {
            registration.Register(services);
        }
    }

    // Builds the host through the setup method and runs its own Configure without a server, Start, or Run.
    private static IWebHost BuildConfiguredSetupMethodHost(
        string environmentName,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = PvgServiceApiHost
            .CreatePvgServiceWebHostBuilder(environmentName, EmptyConfiguration(), SetupMethodUrls, configureServices)
            .Build();

        var startup = host.Services.GetRequiredService<IStartup>();
        var app = host.Services.GetRequiredService<IApplicationBuilderFactory>().CreateBuilder(new FeatureCollection());
        startup.Configure(app);
        app.Build();

        return host;
    }

    private static RouteEndpoint[] MappedEndpoints(IWebHost host) =>
        host.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToArray();

    // Readiness measurement path: the request goes through the mapped GET /health/ready endpoint of the host.
    private static async Task<(int StatusCode, string Body)> InvokeMappedHealthReadyAsync(IWebHost host)
    {
        var endpoint = Assert.Single(
            MappedEndpoints(host),
            candidate =>
                candidate.RoutePattern.RawText == "/health/ready" &&
                (candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Get) ?? false));
        Assert.NotNull(endpoint.RequestDelegate);

        using var scope = host.Services.CreateScope();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/health/ready";
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static void AssertNoTypeNames(string text)
    {
        Assert.DoesNotContain("IPvg", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diten.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", text, StringComparison.Ordinal);
    }

    private static string ProgramSourcePath([CallerFilePath] string testSourcePath = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testSourcePath)!,
            "..",
            "..",
            "src",
            "Diten.PvgService.Api",
            "Program.cs"));

    private static int CountMatches(string code, string pattern) => Regex.Matches(code, pattern).Count;

    // Removes line comments, block comments, and string / char literals so only code is counted.
    private static string StripCommentsAndStringLiterals(string source)
    {
        var code = new StringBuilder(source.Length);
        var index = 0;

        while (index < source.Length)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                while (index < source.Length && source[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            if (current == '/' && next == '*')
            {
                var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? source.Length : end + 2;
                code.Append(' ');
                continue;
            }

            if (current is '"' or '$' or '@')
            {
                var start = index;
                var verbatim = false;
                while (index < source.Length && source[index] is '$' or '@')
                {
                    verbatim |= source[index] == '@';
                    index++;
                }

                if (index < source.Length && source[index] == '"')
                {
                    index = SkipStringLiteral(source, index, verbatim);
                    code.Append("\"\"");
                }
                else
                {
                    code.Append(source, start, index - start);
                }

                continue;
            }

            if (current == '\'')
            {
                index++;
                while (index < source.Length && source[index] != '\'')
                {
                    index += source[index] == '\\' ? 2 : 1;
                }

                index++;
                code.Append("' '");
                continue;
            }

            code.Append(current);
            index++;
        }

        return code.ToString();
    }

    private static int SkipStringLiteral(string source, int quoteIndex, bool verbatim)
    {
        var quoteCount = 0;
        while (quoteIndex + quoteCount < source.Length && source[quoteIndex + quoteCount] == '"')
        {
            quoteCount++;
        }

        if (!verbatim && quoteCount >= 3)
        {
            var delimiter = new string('"', quoteCount);
            var end = source.IndexOf(delimiter, quoteIndex + quoteCount, StringComparison.Ordinal);
            return end < 0 ? source.Length : end + quoteCount;
        }

        var index = quoteIndex + 1;
        while (index < source.Length)
        {
            if (verbatim && source[index] == '"' && index + 1 < source.Length && source[index + 1] == '"')
            {
                index += 2;
                continue;
            }

            if (source[index] == '"')
            {
                return index + 1;
            }

            if (!verbatim && source[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (!verbatim && source[index] == '\n')
            {
                return index;
            }

            index++;
        }

        return source.Length;
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
            [new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, null)],
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
            [new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, null)],
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

    private sealed class TestFieldSecurityPolicy : IPvgFieldSecurityPolicy
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgFieldSecurityRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkflowTransitionGate : IPvgWorkflowTransitionGate
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgWorkflowTransitionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestEvidenceLinkPort : IPvgEvidenceLinkPort
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgEvidenceLinkRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestPermissionGate : IPvgPermissionGate
    {
        public ValueTask<Diten.PvgService.Application.RegPvBase.PvgPermissionDecision> EvaluateAsync(
            PvgPermissionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestIntakeDraftStore : IPvgIntakeDraftStore
    {
        public ValueTask<string> AddAsync(
            PvgPersistenceTenantScope tenantScope,
            SafetyCaseIntake intake,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<int> CountAsync(
            PvgPersistenceTenantScope tenantScope,
            PvgIntakeStatus? status,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(
            PvgPersistenceTenantScope tenantScope,
            string intakeDraftId,
            long? expectedVersion,
            SafetyCaseIntake intake,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
            PvgPersistenceTenantScope tenantScope,
            string intakeDraftId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
            PvgPersistenceListScope listScope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    // ---- WP-0230-DL3B (AC-9): static scan of configuration files for the Pvg:Runtime:* flags ----

    private const string OperationalFlagKey = "Pvg__Runtime__OperationalRuntimeAuthorized";
    private const string AdaptersFlagKey = "Pvg__Runtime__UseNonProductionAdapters";
    private const string ProductionLikeFlagKey = "Pvg__Runtime__ProductionLike";

    // The scan root is the worktree that holds this test source file (four levels above its directory); no .git lookup.
    private static string FindRepositoryRoot([CallerFilePath] string testSourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testSourcePath)!, "..", "..", "..", ".."));

    private static string TestSourceDirectory([CallerFilePath] string testSourcePath = "") =>
        Path.GetDirectoryName(testSourcePath)!;

    [Fact]
    public void Repository_configuration_files_never_enable_operational_runtime_or_production_non_production_adapters()
    {
        var result = PvgConfigurationScanner.Scan(FindRepositoryRoot());

        Assert.NotEmpty(result.ScannedFiles);
        Assert.True(
            result.Violations.Count == 0,
            "Configuration scan violations (file · scope · rule): " +
            string.Join("; ", result.Violations.Select(violation =>
                $"{violation.File} · {violation.Scope} · {violation.Rule}")));
    }

    [Fact]
    public void Repository_configuration_scan_covers_committed_configuration_files_of_the_current_worktree()
    {
        var scanned = PvgConfigurationScanner.Scan(FindRepositoryRoot()).ScannedFiles;

        Assert.Contains(".claude/launch.json", scanned);
        Assert.Contains(".github/workflows/service-tests.yml", scanned);
        Assert.Contains("gateway/Diten.ApiGateway/appsettings.json", scanned);
        Assert.Contains("gateway/Diten.ApiGateway/Properties/launchSettings.json", scanned);
        Assert.Contains("frontend/Diten.Web/appsettings.Development.json", scanned);
        Assert.DoesNotContain(scanned, path => path.StartsWith(".claude/worktrees/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(scanned, path => path.StartsWith("docs/records/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            scanned,
            path => path.Split('/')[..^1].Any(part => part is "bin" or "obj" or "TestResults" or "node_modules" or ".git"));
    }

    [Fact]
    public void Configuration_scan_root_is_the_current_worktree_root()
    {
        var root = FindRepositoryRoot();

        Assert.Equal(Path.GetFullPath(TestSourceDirectory() + "/../../../.."), root);
        Assert.True(File.Exists(Path.Combine(root, "AGENTS.md")));
        Assert.True(Directory.Exists(Path.Combine(root, "services", "Diten.PvgService")));
        Assert.True(
            File.Exists(Path.Combine(root, ".git")) || Directory.Exists(Path.Combine(root, ".git")),
            "The scan root must itself hold .git (a file in a worktree).");
    }

    [Theory]
    [InlineData("appsettings.json", true)]
    [InlineData("src/x/appsettings.Production.json", true)]
    [InlineData("src/x/Properties/launchSettings.json", true)]
    [InlineData(".claude/launch.json", true)]
    [InlineData(".vscode/launch.json", true)]
    [InlineData(".github/workflows/ci.yml", true)]
    [InlineData(".github/workflows/ci.yaml", true)]
    [InlineData("Dockerfile", true)]
    [InlineData("src/x/Dockerfile.api", true)]
    [InlineData("docker-compose.yml", true)]
    [InlineData("docker-compose.override.yaml", true)]
    [InlineData("compose.yaml", true)]
    [InlineData("deploy/x.yml", true)]
    [InlineData("deployment/x.yaml", true)]
    [InlineData("k8s/x.yaml", true)]
    [InlineData("helm/x/values.yaml", true)]
    [InlineData("charts/x/values.yaml", true)]
    [InlineData(".env", true)]
    [InlineData("src/x/prod.env", true)]
    [InlineData("bin/Debug/appsettings.json", false)]
    [InlineData("src/x/obj/appsettings.json", false)]
    [InlineData("TestResults/appsettings.json", false)]
    [InlineData("node_modules/p/appsettings.json", false)]
    [InlineData(".claude/worktrees/w/appsettings.json", false)]
    [InlineData(".git/config", false)]
    [InlineData(".git/appsettings.json", false)]
    [InlineData("src/x/.git/appsettings.json", false)]
    [InlineData("src/x/data.json", false)]
    [InlineData("docs/x.yml", false)]
    [InlineData(".github/x.yml", false)]
    public void Configuration_scan_walks_and_selects_required_file_kinds(string relativePath, bool selected)
    {
        using var fixture = new ScanFixture();
        fixture.Write(relativePath, NeutralScanContent(relativePath));

        var result = PvgConfigurationScanner.Scan(fixture.Root);

        Assert.Equal(selected, result.ScannedFiles.Contains(relativePath));
        Assert.Empty(result.Violations);
    }

    // Fixture notation: a single quote in the content stands for a double quote.
    [Theory]
    [InlineData("O1", "appsettings.Development.json", "{'Pvg':{'Runtime':{'OperationalRuntimeAuthorized':true}}}", true)]
    [InlineData("O2", "appsettings.json", "{'Pvg:Runtime:OperationalRuntimeAuthorized':'true'}", true)]
    [InlineData("O3", "appsettings.json", "{'pvg':{'runtime:operationalruntimeauthorized':'TRUE'}}", true)]
    [InlineData("O4", "Properties/launchSettings.json", "{'profiles':{'dev':{'environmentVariables':{'ASPNETCORE_ENVIRONMENT':'Development','" + OperationalFlagKey + "':'true'}}}}", true)]
    [InlineData("O5", "Properties/launchSettings.json", "{'profiles':{'dev':{'commandLineArgs':'--urls http://127.0.0.1:5011 --Pvg:Runtime:OperationalRuntimeAuthorized=true','environmentVariables':{'ASPNETCORE_ENVIRONMENT':'Development'}}}}", true)]
    [InlineData("O6", ".claude/launch.json", "{'configurations':[{'name':'pvg','runtimeExecutable':'dotnet','runtimeArgs':['run','--','--Pvg:Runtime:OperationalRuntimeAuthorized','true']}]}", true)]
    [InlineData("O7", ".vscode/launch.json", "// yorum\n{'configurations':[{'name':'pvg','env':{'" + OperationalFlagKey + "':'true'}}]}", true)]
    [InlineData("O8", ".github/workflows/ci.yml", "jobs:\n  b:\n    env:\n      " + OperationalFlagKey + ": true\n", true)]
    [InlineData("O9", ".github/workflows/ci.yml", "jobs:\n  b:\n    steps:\n      - run: echo '" + OperationalFlagKey + "=true' >> '$GITHUB_ENV'\n", true)]
    [InlineData("O10", ".github/workflows/ci.yml", "jobs:\n  b:\n    env:\n      " + OperationalFlagKey + ": ${{ vars.PVG_FLAG }}\n", true)]
    [InlineData("O11", "docker-compose.yml", "services:\n  api:\n    environment:\n      - " + OperationalFlagKey + "=True\n", true)]
    [InlineData("O12", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_" + OperationalFlagKey + "=true\n", true)]
    [InlineData("O13", "k8s/deployment.yaml", "env:\n  - name: " + OperationalFlagKey + "\n    value: 'true'\n", true)]
    [InlineData("O14", "Dockerfile", "FROM scratch\nENV " + OperationalFlagKey + " true\n", true)]
    [InlineData("O15", "src/x/Dockerfile.api", "FROM scratch\nENTRYPOINT ['dotnet', 'Diten.PvgService.Api.dll', '--Pvg:Runtime:OperationalRuntimeAuthorized=true']\n", true)]
    [InlineData("O16", ".env", OperationalFlagKey + " = 'true'\n", true)]
    [InlineData("O17", "appsettings.json", "{'Pvg':{'Runtime':{'OperationalRuntimeAuthorized':false}}}", false)]
    [InlineData("O18", "docker-compose.yml", "services:\n  api:\n    environment:\n      - " + OperationalFlagKey + "=false\n", false)]
    [InlineData("O19", "Dockerfile", "FROM scratch\nENV " + OperationalFlagKey + " FALSE\n", false)]
    [InlineData("O20", "appsettings.json", "{'Other':{'Runtime':{'OperationalRuntimeAuthorized':true}}}", false)]
    [InlineData("O21", "k8s/deployment.yaml", "env:\n  - name: " + OperationalFlagKey + "\n    value: 'false'\n", false)]
    [InlineData("O22", ".claude/launch.json", "{'configurations':[{'name':'pvg','runtimeArgs':['run','--','--Pvg:Runtime:OperationalRuntimeAuthorized','false']}]}", false)]
    public void Configuration_scan_flags_operational_runtime_flag_in_every_supported_form(
        string caseId,
        string relativePath,
        string content,
        bool isViolation)
    {
        using var fixture = new ScanFixture();
        fixture.Write(relativePath, content.Replace('\'', '"'));

        var result = PvgConfigurationScanner.Scan(fixture.Root);

        Assert.True(result.ScannedFiles.Contains(relativePath), $"{caseId}: {relativePath} was not scanned.");
        if (isViolation)
        {
            var violation = Assert.Single(result.Violations);
            Assert.Equal(relativePath, violation.File);
            Assert.Equal("OperationalRuntimeAuthorized", violation.Rule);
        }
        else
        {
            Assert.Empty(result.Violations);
        }
    }

    // Fixture notation: a single quote in the content stands for a double quote; scope null = not asserted.
    [Theory]
    [InlineData("U1", "appsettings.json", "{'Pvg':{'Runtime':{'UseNonProductionAdapters':true}}}", true, null)]
    [InlineData("U2", "appsettings.Production.json", "{'Pvg:Runtime:UseNonProductionAdapters':false}", true, null)]
    [InlineData("U3", "appsettings.Development.json", "{'Pvg':{'Runtime':{'ProductionLike':true,'UseNonProductionAdapters':true}}}", true, null)]
    [InlineData("U4", "docker-compose.yml", "services:\n  api:\n    environment:\n      - " + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U5", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=Production\n      - " + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U6", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=Development\n      - " + ProductionLikeFlagKey + "=true\n      - " + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U7", "docker-compose.yml", "services:\n  api:\n    environment:\n      - DOTNET_ENVIRONMENT=Development\n      - " + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U8", "Dockerfile", "FROM scratch\nENV " + AdaptersFlagKey + " true\n", true, null)]
    [InlineData("U9", "k8s/deployment.yaml", "env:\n  - name: " + AdaptersFlagKey + "\n    value: 'true'\n", true, null)]
    [InlineData("U10", ".github/workflows/ci.yml", "jobs:\n  b:\n    env:\n      " + AdaptersFlagKey + ": true\n", true, null)]
    [InlineData("U11", ".github/workflows/ci.yml", "jobs:\n  a:\n    env:\n      ASPNETCORE_ENVIRONMENT: Development\n      " + AdaptersFlagKey + ": true\n  b:\n    env:\n      ASPNETCORE_ENVIRONMENT: Production\n", true, null)]
    [InlineData("U12", ".github/workflows/ci.yml", "jobs:\n  b:\n    env:\n      ASPNETCORE_ENVIRONMENT: ${{ vars.ENV_NAME }}\n      " + AdaptersFlagKey + ": true\n", true, null)]
    [InlineData("U13", "Properties/launchSettings.json", "{'profiles':{'bare':{'environmentVariables':{'" + AdaptersFlagKey + "':'true'}}}}", true, null)]
    [InlineData("U14", "Properties/launchSettings.json", "{'profiles':{'dev':{'environmentVariables':{'ASPNETCORE_ENVIRONMENT':'Development','" + AdaptersFlagKey + "':'true'}},'bare':{'environmentVariables':{'" + AdaptersFlagKey + "':'true'}}}}", true, "bare")]
    [InlineData("U15", "Properties/launchSettings.json", "{'profiles':{'dotnet':{'environmentVariables':{'DOTNET_ENVIRONMENT':'Development','" + AdaptersFlagKey + "':'true'}}}}", true, null)]
    [InlineData("U16", "Properties/launchSettings.json", "{'profiles':{'plike':{'environmentVariables':{'ASPNETCORE_ENVIRONMENT':'Development','" + ProductionLikeFlagKey + "':'true','" + AdaptersFlagKey + "':'true'}}}}", true, null)]
    [InlineData("U17", ".vscode/launch.json", "{'configurations':[{'name':'pvg','env':{'" + AdaptersFlagKey + "':'true'}}]}", true, null)]
    [InlineData("U18", ".claude/launch.json", "{'configurations':[{'name':'pvg','runtimeArgs':['run','--','--Pvg:Runtime:UseNonProductionAdapters=true']}]}", true, null)]
    [InlineData("U19", ".env", AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U20", "appsettings.Development.json", "{'Pvg':{'Runtime':{'UseNonProductionAdapters':true}}}", false, null)]
    [InlineData("U21", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=Development\n      - " + AdaptersFlagKey + "=true\n", false, null)]
    [InlineData("U22", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=Staging\n      - " + AdaptersFlagKey + "=true\n", false, null)]
    [InlineData("U23", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=Development\n      - " + ProductionLikeFlagKey + "=false\n      - " + AdaptersFlagKey + "=true\n", false, null)]
    [InlineData("U24", "Properties/launchSettings.json", "{'profiles':{'dev':{'environmentVariables':{'ASPNETCORE_ENVIRONMENT':'Development','" + AdaptersFlagKey + "':'true'}}}}", false, null)]
    [InlineData("U25", ".github/workflows/ci.yml", "jobs:\n  b:\n    env:\n      ASPNETCORE_ENVIRONMENT: Development\n      " + AdaptersFlagKey + ": true\n", false, null)]
    [InlineData("U26", ".claude/launch.json", "{'configurations':[{'name':'pvg','env':{'ASPNETCORE_ENVIRONMENT':'Development'},'runtimeArgs':['run','--','--Pvg:Runtime:UseNonProductionAdapters=true']}]}", false, null)]
    [InlineData("U27", "Properties/launchSettings.json", "{'profiles':{'empty':{'environmentVariables':{'ASPNETCORE_ENVIRONMENT':'','" + AdaptersFlagKey + "':'true'}}}}", true, "empty")]
    [InlineData("U28", ".github/workflows/ci.yml", "jobs:\n  b:\n    env:\n      ASPNETCORE_ENVIRONMENT: ''\n      " + AdaptersFlagKey + ": true\n", true, null)]
    [InlineData("U29", "docker-compose.yml", "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=production\n      - " + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U30", "docker-compose.yml", "services:\n  api:\n    command: --ASPNETCORE_ENVIRONMENT=Development\n    environment:\n      - " + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U31", ".env", "# ASPNETCORE_ENVIRONMENT is Development\n" + AdaptersFlagKey + "=true\n", true, null)]
    [InlineData("U32", ".claude/launch.json", "{'configurations':[{'name':'pvg','runtimeArgs':['run','--','--ASPNETCORE_ENVIRONMENT=Development','--Pvg:Runtime:UseNonProductionAdapters=true']}]}", true, null)]
    public void Configuration_scan_flags_non_production_adapters_only_for_production_like_scope(
        string caseId,
        string relativePath,
        string content,
        bool isViolation,
        string? expectedScope)
    {
        using var fixture = new ScanFixture();
        fixture.Write(relativePath, content.Replace('\'', '"'));

        var result = PvgConfigurationScanner.Scan(fixture.Root);

        Assert.True(result.ScannedFiles.Contains(relativePath), $"{caseId}: {relativePath} was not scanned.");
        if (isViolation)
        {
            var violation = Assert.Single(result.Violations);
            Assert.Equal(relativePath, violation.File);
            Assert.Equal("NonProductionAdaptersInProductionLikeScope", violation.Rule);
            if (expectedScope is not null)
            {
                Assert.Equal(expectedScope, violation.Scope);
            }
        }
        else
        {
            Assert.Empty(result.Violations);
        }
    }

    [Fact]
    public void Program_environment_name_comes_only_from_aspnetcore_environment_variable()
    {
        var expected = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Production;

        var actual = Program.CreateWebHostBuilder([]).GetSetting(WebHostDefaults.EnvironmentKey);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Program_source_reads_environment_name_only_from_aspnetcore_environment_variable_text()
    {
        var programPath = ProgramSourcePath();
        Assert.True(File.Exists(programPath), $"Program source not found at '{programPath}'.");

        var source = File.ReadAllText(programPath);

        Assert.DoesNotContain("DOTNET_ENVIRONMENT", source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            1,
            CountMatches(
                source,
                @"\bvar\s+environmentName\s*=\s*Environment\s*\.\s*GetEnvironmentVariable\s*\(\s*""ASPNETCORE_ENVIRONMENT""\s*\)\s*;"));
        Assert.Equal(1, CountMatches(source, @"\bCreatePvgServiceWebHostBuilder\s*\(\s*environmentName\s*,"));
    }

    [Fact]
    public void Configuration_scan_flag_pattern_is_culture_invariant()
    {
        Assert.True(PvgConfigurationScanner.FlagReferenceOptions.HasFlag(RegexOptions.CultureInvariant));
        Assert.True(PvgConfigurationScanner.FlagReferenceOptions.HasFlag(RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Configuration_scan_matches_upper_case_flag_key_under_turkish_culture()
    {
        using var fixture = new ScanFixture();
        fixture.Write(".env", "PVG__RUNTIME__OPERATIONALRUNTIMEAUTHORIZED=true\n");
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var violation = Assert.Single(PvgConfigurationScanner.Scan(fixture.Root).Violations);

            Assert.Equal("OperationalRuntimeAuthorized", violation.Rule);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Configuration_scan_treats_upper_case_production_environment_name_as_production_under_turkish_culture()
    {
        using var fixture = new ScanFixture();
        fixture.Write(
            "docker-compose.yml",
            "services:\n  api:\n    environment:\n      - ASPNETCORE_ENVIRONMENT=PRODUCTION\n      - " +
            AdaptersFlagKey + "=true\n");

        var violation = ScanUnderTurkishCulture(fixture.Root).Single();

        Assert.Equal("NonProductionAdaptersInProductionLikeScope", violation.Rule);
    }

    [Fact]
    public void Configuration_scan_matches_upper_case_adapters_flag_name_under_turkish_culture()
    {
        using var fixture = new ScanFixture();
        fixture.Write(".env", "PVG__RUNTIME__USENONPRODUCTIONADAPTERS=true\n");

        var violation = ScanUnderTurkishCulture(fixture.Root).Single();

        Assert.Equal("NonProductionAdaptersInProductionLikeScope", violation.Rule);
    }

    // Runs the scan with the current culture set to tr-TR (dotted and dotless i) and restores it afterwards.
    private static IReadOnlyList<PvgConfigurationScanner.ScanViolation> ScanUnderTurkishCulture(string root)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            return PvgConfigurationScanner.Scan(root).Violations;
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Configuration_scan_excludes_docs_records_tree()
    {
        using var fixture = new ScanFixture();
        fixture.Write("appsettings.json", "{}");
        fixture.Write("docs/records/a/lane.env", OperationalFlagKey + "=true\n");
        fixture.Write("docs/records/a/appsettings.json", "{\"Pvg\":{\"Runtime\":{\"OperationalRuntimeAuthorized\":true}}}");

        var result = PvgConfigurationScanner.Scan(fixture.Root);

        Assert.Equal(new[] { "appsettings.json" }, result.ScannedFiles);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Configuration_scan_fails_closed_on_unparseable_json()
    {
        using var fixture = new ScanFixture();
        fixture.Write("src/x/appsettings.json", "{");

        var exception = Assert.Throws<InvalidOperationException>(() => PvgConfigurationScanner.Scan(fixture.Root));

        Assert.Contains("src/x/appsettings.json", exception.Message);
    }

    [Fact]
    public void Configuration_scan_fails_closed_on_inaccessible_directory()
    {
        using var fixture = new ScanFixture();
        fixture.Write("locked/appsettings.json", "{}");
        var locked = Path.Combine(fixture.Root, "locked");

        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            var precondition = Record.Exception(() => Directory.EnumerateFileSystemEntries(locked).ToArray());
            Assert.True(
                precondition is UnauthorizedAccessException,
                "The runner ignores directory permissions (for example root); the inaccessible-directory scan cannot be measured here.");

            Assert.ThrowsAny<Exception>(() => PvgConfigurationScanner.Scan(fixture.Root));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static string NeutralScanContent(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return "{}";
        }

        if (name.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
        {
            return "A=1";
        }

        if (name.Contains("Dockerfile", StringComparison.OrdinalIgnoreCase))
        {
            return "FROM scratch";
        }

        return name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            ? "x: 1"
            : "x";
    }

    // Writes scan inputs under a temp "pvg-dl3b-<guid>" directory; the scan root sits below a ".claude/worktrees"
    // path so a root that itself lies under a worktrees directory is measured. Nothing is written to the repository.
    private sealed class ScanFixture : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "pvg-dl3b-" + Guid.NewGuid().ToString("N"));

        public ScanFixture()
        {
            Root = Path.Combine(_directory, ".claude", "worktrees", "fixture-root");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temp directory.
            }
        }
    }

    // Scans configuration-like files below a root for two Pvg:Runtime:* flags and returns the files it looked at
    // (root-relative, "/" separated, ordinal order) and the violations. Every test goes through Scan(root).
    //   OperationalRuntimeAuthorized: any reference whose value is not a plain "false" is a violation (fail-closed).
    //   NonProductionAdaptersInProductionLikeScope: UseNonProductionAdapters in a production-like scope, any value.
    private static class PvgConfigurationScanner
    {
        public const string OperationalRule = "OperationalRuntimeAuthorized";
        public const string AdaptersRule = "NonProductionAdaptersInProductionLikeScope";

        public sealed record ScanViolation(string File, string Scope, string Rule);

        public sealed record ScanResult(IReadOnlyList<string> ScannedFiles, IReadOnlyList<ScanViolation> Violations);

        private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "TestResults", "node_modules", ".git"
        };

        // Compared on the root-relative path, never on the absolute path (the running worktree itself lies below
        // a ".claude/worktrees" directory).
        private static readonly string[] ExcludedRelativeDirectories = [".claude/worktrees", "docs/records"];

        private static readonly string[] DeploymentDirectoryParts =
            ["deploy", "deployment", "deployments", "k8s", "kubernetes", "helm", "charts"];

        private const string Token =
            @"(?:\$\{\{.*?\}\}|\$\{[^}\r\n]*\}|\$\([^)\r\n]*\)|""[^""\r\n]*""|'[^'\r\n]*'|[^\s,""'\]\}\)#;&|<>]+)";

        // Key reference: the text "Pvg" + separator + "Runtime" + separator + name, whatever precedes it.
        // A Regex fixes its case-insensitivity tables when it is constructed, so one instance is kept per current
        // culture; CultureInvariant keeps the match independent of that culture (for example tr-TR dotted i).
        private static readonly ConcurrentDictionary<string, Regex> FlagReferenceByCulture = new();

        private static Regex FlagReference =>
            FlagReferenceByCulture.GetOrAdd(
                CultureInfo.CurrentCulture.Name,
                _ => new Regex(
                    @"Pvg(?::|__)Runtime(?::|__)(?<name>OperationalRuntimeAuthorized|UseNonProductionAdapters|ProductionLike)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

        public static RegexOptions FlagReferenceOptions => FlagReference.Options;

        private static readonly Regex EnvironmentReference = new(
            @"(?<![A-Za-z0-9_-])ASPNETCORE_ENVIRONMENT(?![A-Za-z0-9_])",
            RegexOptions.CultureInvariant);

        // "key: v", "key=v", "key = v", "key, v" (a = separated), or "key v" (b = whitespace only).
        private static readonly Regex ValueAfterKey = new(
            @"\G[""']?(?:(?<a>[ \t]*[:=,][ \t]*)|(?<b>[ \t]+))(?<tok>" + Token + ")?",
            RegexOptions.CultureInvariant);

        private static readonly Regex KubernetesNamePrefix = new(
            @"^(?<lead>[ \t]*(?:-[ \t]+)?)name[ \t]*:[ \t]*[""']?$",
            RegexOptions.CultureInvariant);

        private static readonly Regex KubernetesValueLine = new(
            @"^value[ \t]*:[ \t]*(?<tok>" + Token + ")?",
            RegexOptions.CultureInvariant);

        private static readonly Regex PlainEnvironmentName = new(
            @"^[A-Za-z0-9_.-]+$",
            RegexOptions.CultureInvariant);

        private static readonly EnumerationOptions WalkOptions = new()
        {
            // The .NET default is IgnoreInaccessible = true and AttributesToSkip = Hidden | System (which hides
            // every dot-file and dot-directory on Unix); both must be set explicitly.
            IgnoreInaccessible = false,
            AttributesToSkip = 0,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        public static ScanResult Scan(string root)
        {
            var files = new List<string>();
            Walk(new DirectoryInfo(root), string.Empty, files);
            files.Sort(StringComparer.Ordinal);

            var scanned = files.Where(IsSelected).ToList();
            var violations = new List<ScanViolation>();
            foreach (var relativePath in scanned)
            {
                violations.AddRange(AnalyzeFile(relativePath, File.ReadAllText(Path.Combine(root, relativePath))));
            }

            return new ScanResult(scanned, violations);
        }

        private static void Walk(DirectoryInfo directory, string relativePath, List<string> files)
        {
            if (IsExcludedDirectory(directory, relativePath))
            {
                return;
            }

            foreach (var entry in directory.EnumerateFileSystemInfos("*", WalkOptions))
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var entryPath = relativePath.Length == 0 ? entry.Name : relativePath + "/" + entry.Name;
                if (entry is DirectoryInfo subdirectory)
                {
                    Walk(subdirectory, entryPath, files);
                }
                else if (!string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(entryPath);
                }
            }
        }

        private static bool IsExcludedDirectory(DirectoryInfo directory, string relativePath) =>
            (relativePath.Length > 0 && ExcludedDirectoryNames.Contains(directory.Name)) ||
            ExcludedRelativeDirectories.Any(excluded =>
                string.Equals(relativePath, excluded, StringComparison.OrdinalIgnoreCase));

        // ---- selection ----

        private static bool IsSelected(string relativePath)
        {
            var name = relativePath[(relativePath.LastIndexOf('/') + 1)..];
            return IsAppSettingsFile(name) ||
                   IsLaunchFile(relativePath, name) ||
                   IsWorkflowFile(relativePath) ||
                   IsDockerfile(name) ||
                   IsComposeFile(name) ||
                   IsEnvFile(name) ||
                   IsDeploymentDirectoryFile(relativePath, name);
        }

        private static bool IsAppSettingsFile(string name) =>
            name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
            name.Length >= "appsettings.json".Length;

        private static bool IsLaunchFile(string relativePath, string name) =>
            string.Equals(name, "launchSettings.json", StringComparison.OrdinalIgnoreCase) ||
            relativePath.EndsWith(".claude/launch.json", StringComparison.OrdinalIgnoreCase) ||
            relativePath.EndsWith(".vscode/launch.json", StringComparison.OrdinalIgnoreCase);

        private static bool IsWorkflowFile(string relativePath) =>
            relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) &&
            IsYamlFile(relativePath);

        private static bool IsDockerfile(string name) =>
            string.Equals(name, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".Dockerfile", StringComparison.OrdinalIgnoreCase);

        private static bool IsComposeFile(string name) =>
            (name.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) && IsYamlFile(name)) ||
            string.Equals(name, "compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            (name.StartsWith("compose.", StringComparison.OrdinalIgnoreCase) && IsYamlFile(name));

        private static bool IsEnvFile(string name) =>
            string.Equals(name, ".env", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(name), ".env", StringComparison.OrdinalIgnoreCase);

        private static bool IsDeploymentDirectoryFile(string relativePath, string name) =>
            (IsYamlFile(name) || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) &&
            relativePath.Split('/')[..^1].Any(part =>
                DeploymentDirectoryParts.Contains(part, StringComparer.OrdinalIgnoreCase));

        private static bool IsYamlFile(string name) =>
            name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

        // ---- analysis ----

        // The values found for each flag in one scope; a null value means "present but not determinable".
        private sealed class ScopeFacts(string name)
        {
            public string Name { get; } = name;

            public List<string?> OperationalValues { get; } = [];

            public List<string?> AdapterValues { get; } = [];

            public List<string?> ProductionLikeValues { get; } = [];

            public List<string?> EnvironmentValues { get; } = [];

            public void AddFlag(string flagName, string? value)
            {
                if (string.Equals(flagName, "OperationalRuntimeAuthorized", StringComparison.OrdinalIgnoreCase))
                {
                    OperationalValues.Add(value);
                }
                else if (string.Equals(flagName, "UseNonProductionAdapters", StringComparison.OrdinalIgnoreCase))
                {
                    AdapterValues.Add(value);
                }
                else
                {
                    ProductionLikeValues.Add(value);
                }
            }
        }

        private static IEnumerable<ScanViolation> AnalyzeFile(string relativePath, string text)
        {
            var name = relativePath[(relativePath.LastIndexOf('/') + 1)..];
            var scopes = new Dictionary<string, ScopeFacts>(StringComparer.Ordinal);

            ScopeFacts ScopeOf(string scopeName)
            {
                if (!scopes.TryGetValue(scopeName, out var facts))
                {
                    scopes[scopeName] = facts = new ScopeFacts(scopeName);
                }

                return facts;
            }

            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var root = ParseJson(relativePath, text);
                if (IsLaunchFile(relativePath, name))
                {
                    AnalyzeLaunchFile(root, name, ScopeOf);
                }
                else
                {
                    var facts = ScopeOf("file");
                    AddJson(facts, root, readEnvironmentNames: !IsAppSettingsFile(name));
                    if (IsAppSettingsFile(name))
                    {
                        var environmentName = AppSettingsEnvironmentName(name);
                        if (environmentName is not null)
                        {
                            facts.EnvironmentValues.Add(environmentName);
                        }
                    }
                }
            }
            else
            {
                AddText(ScopeOf("file"), text, readEnvironmentNames: true);
            }

            var violations = new List<ScanViolation>();
            foreach (var scope in scopes.Values)
            {
                if (scope.OperationalValues.Any(value => !IsPlainFalse(value)))
                {
                    violations.Add(new ScanViolation(relativePath, scope.Name, OperationalRule));
                }

                if (scope.AdapterValues.Count > 0 && IsProductionLike(scope))
                {
                    violations.Add(new ScanViolation(relativePath, scope.Name, AdaptersRule));
                }
            }

            return violations;
        }

        private static JsonElement ParseJson(string relativePath, string text)
        {
            try
            {
                using var document = JsonDocument.Parse(
                    text,
                    new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                return document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"Configuration file '{relativePath}' is not parseable JSON: {exception.Message}",
                    exception);
            }
        }

        // launchSettings.json: every "profiles" property is a scope; .claude/launch.json and .vscode/launch.json:
        // every "configurations" item is a scope (named by "name"). Anything else in the file is the "file" scope.
        private static void AnalyzeLaunchFile(JsonElement root, string fileName, Func<string, ScopeFacts> scopeOf)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                AddJson(scopeOf("file"), root, readEnvironmentNames: false);
                return;
            }

            var isLaunchSettings = string.Equals(fileName, "launchSettings.json", StringComparison.OrdinalIgnoreCase);
            var scopeProperty = isLaunchSettings ? "profiles" : "configurations";
            var environmentProperty = isLaunchSettings ? "environmentVariables" : "env";

            foreach (var property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, scopeProperty, StringComparison.OrdinalIgnoreCase))
                {
                    AddJson(scopeOf("file"), property.Value, readEnvironmentNames: false, property.Name);
                    continue;
                }

                if (isLaunchSettings && property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var profile in property.Value.EnumerateObject())
                    {
                        AddLaunchScope(scopeOf(profile.Name), profile.Value, environmentProperty);
                    }
                }
                else if (!isLaunchSettings && property.Value.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var configuration in property.Value.EnumerateArray())
                    {
                        var scopeName =
                            configuration.ValueKind == JsonValueKind.Object &&
                            TryGetProperty(configuration, "name", out var nameValue) &&
                            nameValue.ValueKind == JsonValueKind.String
                                ? nameValue.GetString()!
                                : "configuration-" + index;
                        AddLaunchScope(scopeOf(scopeName), configuration, environmentProperty);
                        index++;
                    }
                }
                else
                {
                    AddJson(scopeOf("file"), property.Value, readEnvironmentNames: false, property.Name);
                }
            }
        }

        private static void AddLaunchScope(ScopeFacts facts, JsonElement scope, string environmentProperty)
        {
            // Flag references anywhere in the scope; the environment name only from the environment object and
            // the argument properties.
            AddJson(facts, scope, readEnvironmentNames: false);

            if (scope.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (TryGetProperty(scope, environmentProperty, out var environment) &&
                environment.ValueKind == JsonValueKind.Object)
            {
                foreach (var variable in environment.EnumerateObject())
                {
                    if (string.Equals(variable.Name, "ASPNETCORE_ENVIRONMENT", StringComparison.Ordinal))
                    {
                        facts.EnvironmentValues.Add(ScalarText(variable.Value));
                    }
                }
            }

            foreach (var argumentProperty in new[] { "commandLineArgs", "args", "runtimeArgs" })
            {
                if (TryGetProperty(scope, argumentProperty, out var arguments))
                {
                    var argumentText = arguments.ValueKind == JsonValueKind.Array
                        ? JoinScalars(arguments)
                        : ScalarText(arguments);
                    if (argumentText is not null)
                    {
                        AddEnvironmentReferences(facts, argumentText);
                    }
                }
            }
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        // Flattens the element ("a:b:0:c" paths, leaf values) and matches flag references both in the paths
        // (JSON keys) and in the text of string values and primitive arrays (command-line style arguments).
        private static void AddJson(
            ScopeFacts facts,
            JsonElement element,
            bool readEnvironmentNames,
            string path = "")
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        AddJson(
                            facts,
                            property.Value,
                            readEnvironmentNames,
                            path.Length == 0 ? property.Name : path + ":" + property.Name);
                    }

                    break;
                case JsonValueKind.Array:
                    var joined = JoinScalars(element);
                    if (joined is not null)
                    {
                        AddText(facts, joined, readEnvironmentNames);
                    }

                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        // Primitive items are already covered by the joined text above.
                        if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        {
                            AddJson(facts, item, readEnvironmentNames, path + ":" + index);
                        }

                        index++;
                    }

                    break;
                default:
                    var value = ScalarText(element);
                    AddPath(facts, path, value);
                    if (readEnvironmentNames &&
                        string.Equals(path.Split(':')[^1], "ASPNETCORE_ENVIRONMENT", StringComparison.Ordinal))
                    {
                        facts.EnvironmentValues.Add(value);
                    }

                    if (element.ValueKind == JsonValueKind.String)
                    {
                        AddText(facts, value!, readEnvironmentNames);
                    }

                    break;
            }
        }

        private static void AddPath(ScopeFacts facts, string path, string? value)
        {
            foreach (Match match in FlagReference.Matches(path))
            {
                facts.AddFlag(match.Groups["name"].Value, value);
            }
        }

        private static string? ScalarText(JsonElement element) =>
            element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => element.GetRawText(),
                _ => null
            };

        // The primitive items of an array joined with single spaces (null when it holds none).
        private static string? JoinScalars(JsonElement array)
        {
            var items = array.EnumerateArray()
                .Select(ScalarText)
                .Where(text => text is not null)
                .ToArray();
            return items.Length == 0 ? null : string.Join(" ", items);
        }

        private static string? AppSettingsEnvironmentName(string fileName)
        {
            // appsettings.json gives no name; appsettings.<X>[.more].json names X.
            var inner = fileName["appsettings".Length..^".json".Length];
            if (!inner.StartsWith('.'))
            {
                return null;
            }

            var first = inner[1..].Split('.')[0];
            return first.Length == 0 ? null : first;
        }

        // Text mode: flag references and (when asked) ASPNETCORE_ENVIRONMENT assignments, each with its value.
        private static void AddText(ScopeFacts facts, string text, bool readEnvironmentNames)
        {
            foreach (Match match in FlagReference.Matches(text))
            {
                facts.AddFlag(match.Groups["name"].Value, ReadValue(text, match.Index, match.Index + match.Length));
            }

            if (!readEnvironmentNames)
            {
                return;
            }

            AddEnvironmentReferences(facts, text);
        }

        // ASPNETCORE_ENVIRONMENT assignments in text. A commented-out mention (a "#" earlier on its line) is not an
        // assignment, and the command-line spelling "--ASPNETCORE_ENVIRONMENT=x" is not matched because Program
        // reads the name from the process environment only.
        private static void AddEnvironmentReferences(ScopeFacts facts, string text)
        {
            foreach (Match match in EnvironmentReference.Matches(text))
            {
                var lineStart = match.Index == 0 ? 0 : text.LastIndexOf('\n', match.Index - 1) + 1;
                if (text.IndexOf('#', lineStart, match.Index - lineStart) >= 0)
                {
                    continue;
                }

                facts.EnvironmentValues.Add(ReadValue(text, match.Index, match.Index + match.Length));
            }
        }

        // The value of the key written at [keyStart, keyEnd): null when it cannot be determined, "" when it is empty.
        private static string? ReadValue(string text, int keyStart, int keyEnd)
        {
            var lineStart = keyStart == 0 ? 0 : text.LastIndexOf('\n', keyStart - 1) + 1;
            var named = KubernetesNamePrefix.Match(text.Substring(lineStart, keyStart - lineStart).TrimEnd('\r'));
            if (named.Success)
            {
                return ReadKubernetesValue(text, keyEnd, named.Groups["lead"].Length);
            }

            var match = ValueAfterKey.Match(text, keyEnd);
            if (match.Groups["a"].Success)
            {
                return match.Groups["tok"].Success ? Unquote(match.Groups["tok"].Value) : string.Empty;
            }

            return match.Groups["b"].Success && match.Groups["tok"].Success
                ? Unquote(match.Groups["tok"].Value)
                : null;
        }

        // Kubernetes env list item: "- name: K" followed by "value: v" at the same indentation.
        private static string? ReadKubernetesValue(string text, int keyEnd, int nameIndent)
        {
            var lineEnd = text.IndexOf('\n', keyEnd);
            if (lineEnd < 0)
            {
                return null;
            }

            foreach (var rawLine in text[(lineEnd + 1)..].Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                var indent = line.Length - line.TrimStart().Length;
                if (indent < nameIndent)
                {
                    return null;
                }

                var valueLine = KubernetesValueLine.Match(line.TrimStart());
                if (indent == nameIndent && valueLine.Success)
                {
                    return valueLine.Groups["tok"].Success ? Unquote(valueLine.Groups["tok"].Value) : string.Empty;
                }
            }

            return null;
        }

        private static string Unquote(string token)
        {
            token = token.Trim();
            return token.Length >= 2 &&
                   ((token[0] == '"' && token[^1] == '"') || (token[0] == '\'' && token[^1] == '\''))
                ? token[1..^1].Trim()
                : token;
        }

        // ---- classification ----

        private static bool IsPlainFalse(string? value) =>
            value is not null && string.Equals(value.Trim(), "false", StringComparison.OrdinalIgnoreCase);

        private static bool IsPlainNonProductionName(string? value) =>
            value is not null &&
            PlainEnvironmentName.IsMatch(value) &&
            !string.Equals(value, "Production", StringComparison.OrdinalIgnoreCase);

        // A scope is production-like unless (P1) every ASPNETCORE_ENVIRONMENT assignment in it is a plain,
        // non-empty, non-Production name (and there is at least one) and (P2) ProductionLike is not enabled.
        // DOTNET_ENVIRONMENT is never read: Program reads only ASPNETCORE_ENVIRONMENT.
        private static bool IsProductionLike(ScopeFacts scope)
        {
            var environmentGiven =
                scope.EnvironmentValues.Count > 0 && scope.EnvironmentValues.All(IsPlainNonProductionName);
            var productionLikeDisabled = scope.ProductionLikeValues.All(IsPlainFalse);
            return !(environmentGiven && productionLikeDisabled);
        }
    }
}
