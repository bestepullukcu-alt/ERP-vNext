using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementContract.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.DocumentManagementContract.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class DocumentManagementContractFoundationTests
{
    private const string CorrelationId = "fu01-correlation-001";

    [Fact]
    public async Task Handler_returns_foundation_only_contract_from_registered_runtime_seams()
    {
        using var services = CreateServices();
        var handler = CreateHandler(services);

        var response = await handler.Handle(new GetDocumentManagementContractQuery(CorrelationId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CorrelationId, response.CorrelationId);
        Assert.Null(response.ReasonCode);
        Assert.Equal("MOD-0028-FU01", response.Data!.ModuleId);
        Assert.Equal("MOD-0028", response.Data.ParentModuleId);
        Assert.Equal(DocumentManagementRoutes.ApiFamily, response.Data.ApiFamily);
        Assert.Equal("FOUNDATION_ONLY", response.Data.BusinessCapabilityStatus);
        Assert.Equal(["CORPORATE", "COMPANY"], response.Data.ActiveScopes);
        Assert.Equal(["POSITION", "PERSON"], response.Data.DeferredScopes);
        Assert.True(response.Data.Mod0220ValidatorRegistered);
        Assert.True(response.Data.AuditCorrelationReady);
        Assert.Equal(DocumentManagementPermissions.RequiredForFu01, response.Data.PermissionsRequiredForFu01);
        Assert.Equal("This endpoint does not indicate full MOD-0028 business readiness.", response.Data.Warning);
    }

    [Fact]
    public void Feature_flag_defaults_keep_position_person_and_optional_integrations_disabled()
    {
        var options = new DocumentManagementFeatureFlagOptions();
        var summary = options.ToSummary();

        Assert.True(summary[DocumentManagementFeatureFlags.CorporateRoot]);
        Assert.True(summary[DocumentManagementFeatureFlags.CompanyProvisioning]);
        Assert.True(summary[DocumentManagementFeatureFlags.ManualLocalNodes]);
        Assert.False(summary[DocumentManagementFeatureFlags.Exceptions]);
        Assert.False(summary[DocumentManagementFeatureFlags.PositionScope]);
        Assert.False(summary[DocumentManagementFeatureFlags.PersonScope]);
        Assert.False(summary[DocumentManagementFeatureFlags.WorkflowIntegration]);
    }

    [Fact]
    public void Controller_exposes_only_the_approved_read_only_route_and_permission()
    {
        var controllerRoute = typeof(DocumentManagementController).GetCustomAttribute<RouteAttribute>();
        var action = typeof(DocumentManagementController).GetMethod(nameof(DocumentManagementController.GetContract))
            ?? throw new InvalidOperationException("Contract action was not found.");

        Assert.Equal(DocumentManagementRoutes.ApiFamily, controllerRoute!.Template);
        Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
        Assert.Equal("contract", action.GetCustomAttribute<HttpGetAttribute>()!.Template);

        var permission = action.GetCustomAttribute<HasPermissionAttribute>();
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(DocumentManagementPermissions.ContractView, field!.GetValue(permission));

        var publicActions = typeof(DocumentManagementController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Single(publicActions);
    }

    [Fact]
    public async Task Controller_returns_response_with_request_correlation_id()
    {
        var expected = Response<DocumentManagementContractResponse>.Success(
            CreateContractResponse(),
            correlationId: CorrelationId);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(It.Is<GetDocumentManagementContractQuery>(query => query.CorrelationId == CorrelationId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelationId(CorrelationId);
        var controller = new DocumentManagementController(mediator.Object, correlationContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetContract(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<Response<DocumentManagementContractResponse>>(ok.Value);
        Assert.Equal(CorrelationId, response.CorrelationId);
    }

    [Fact]
    public async Task Missing_permission_returns_controlled_403_with_reason_and_correlation()
    {
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelationId(CorrelationId);
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICorrelationContext>(correlationContext)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("actor_type", "tenant_user")], "Test")),
            RequestServices = serviceProvider
        };
        var filterContext = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);

        await new HasPermissionAttribute(DocumentManagementPermissions.ContractView)
            .OnAuthorizationAsync(filterContext);

        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var response = Assert.IsType<Response<NoContent>>(result.Value);
        Assert.Equal(DocumentManagementReasonCodes.PermissionDenied, response.ReasonCode);
        Assert.Equal(CorrelationId, response.CorrelationId);
        Assert.DoesNotContain("Exception", response.Errors.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack", response.Errors.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contract_permission_canonical_claim_is_effective()
    {
        var result = PermissionClaimEvaluator.Evaluate(
            [new Claim("permission", DocumentManagementPermissions.ContractView)],
            DocumentManagementPermissions.ContractView);

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.CanonicalMatch, result.Match);
        Assert.False(result.MatchedViaLegacyAlias);
    }

    [Fact]
    public async Task Correlation_middleware_uses_the_same_value_for_header_context_and_body()
    {
        Response<string>? body = null;
        var correlationContext = new CorrelationContext();
        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                body = Response<string>.Success("ok", correlationId: correlationContext.CorrelationId);
                return Task.CompletedTask;
            },
            Options.Create(new ObservabilityOptions()));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-Id"] = CorrelationId;

        await middleware.InvokeAsync(httpContext, correlationContext);

        Assert.Equal(CorrelationId, httpContext.Response.Headers["X-Correlation-Id"]);
        Assert.Equal(CorrelationId, correlationContext.CorrelationId);
        Assert.Equal(CorrelationId, body!.CorrelationId);
    }

    [Fact]
    public void Response_serialization_preserves_existing_shape_and_adds_optional_metadata()
    {
        var success = Response<string>.Success("value", correlationId: CorrelationId);
        var failure = Response<string>.Fail("Controlled failure.", 400, "VALIDATION_FAILED", CorrelationId);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var successJson = JsonSerializer.Serialize(success, jsonOptions);
        var failureJson = JsonSerializer.Serialize(failure, jsonOptions);

        Assert.Contains("\"data\":\"value\"", successJson);
        Assert.Contains("\"statusCode\":200", successJson);
        Assert.Contains("\"isSuccessful\":true", successJson);
        Assert.Contains("\"errors\":[]", successJson);
        Assert.Contains($"\"correlation_id\":\"{CorrelationId}\"", successJson);
        Assert.Contains("\"reason_code\":\"VALIDATION_FAILED\"", failureJson);
        Assert.DoesNotContain("stackTrace", failureJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", failureJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handler_reports_validator_as_missing_without_invoking_a_remote_dependency()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<DocumentManagementFeatureFlagOptions>(_ => { });
        services.AddSingleton<IAuditService, StubAuditService>();
        using var provider = services.BuildServiceProvider();
        var handler = CreateHandler(provider);

        var response = await handler.Handle(new GetDocumentManagementContractQuery(CorrelationId), CancellationToken.None);

        Assert.False(response.Data!.Mod0220ValidatorRegistered);
        Assert.True(response.Data.AuditCorrelationReady);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<DocumentManagementFeatureFlagOptions>(_ => { });
        services.AddSingleton<ILegalEntityReferenceValidator, ThrowingLegalEntityReferenceValidator>();
        services.AddSingleton<IAuditService, StubAuditService>();
        return services.BuildServiceProvider();
    }

    private static GetDocumentManagementContractHandler CreateHandler(IServiceProvider provider) => new(
        provider.GetRequiredService<IOptions<DocumentManagementFeatureFlagOptions>>(),
        provider.GetRequiredService<IServiceProviderIsService>());

    private static DocumentManagementContractResponse CreateContractResponse() => new(
        "MOD-0028-FU01",
        "Documentation Management Backend Contract Foundation",
        "MOD-0028",
        DocumentManagementRoutes.ApiFamily,
        "1.0",
        ["CORPORATE", "COMPANY"],
        ["POSITION", "PERSON"],
        new DocumentManagementFeatureFlagOptions().ToSummary(),
        DocumentManagementPermissions.RequiredForFu01,
        true,
        true,
        "FOUNDATION_ONLY",
        "This endpoint does not indicate full MOD-0028 business readiness.");

    private sealed class ThrowingLegalEntityReferenceValidator : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
            throw new InvalidOperationException("The contract endpoint must not call MOD-0220.");
    }

    private sealed class StubAuditService : IAuditService
    {
        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default) =>
            Task.FromResult(AuditAppendResult.Queued("test"));
    }
}
