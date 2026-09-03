using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductIdentityWorkflowOperationRecoveryApiContractTests
{
    internal const string RecoverPermission = "mdm.product-identity.lifecycle-operations.recover";
    internal static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid SubjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public void Controller_exposes_only_the_exact_recovery_command_route()
    {
        var type = typeof(ProductIdentityWorkflowOperationsController);
        Assert.Equal("api/product-identity-workflow-operations", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .Select(attribute => (Method: method, Attribute: attribute))).ToArray();
        var route = Assert.Single(routes);
        Assert.Equal(nameof(ProductIdentityWorkflowOperationsController.RecoverBeforeStart), route.Method.Name);
        Assert.Equal("{operationId:guid}/recover-before-start", route.Attribute.Template);
        Assert.Equal(["POST"], route.Attribute.HttpMethods);
    }

    [Fact]
    public async Task Valid_request_maps_only_route_header_and_five_business_fields_to_the_command()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var capture = (CapturingMediator)(object)mediator;
        var operationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var controller = Controller(mediator, commandId.ToString("D"));
        await Assert.ThrowsAsync<CapturedRequestException>(() => controller.RecoverBeforeStart(
            operationId,
            Body("""{"action":2,"expectedOperationVersion":3,"expectedTargetVersion":{"primaryEntityVersion":7,"productDefinitionRevisionVersion":5},"reasonCode":"ORPHAN_RECOVERY","comment":"operator approved"}"""),
            default));
        var command = Assert.IsType<RecoverOrphanedProductIdentityWorkflowOperationCommand>(capture.Request);
        Assert.Equal(operationId, command.OperationId);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryAction.Supersede, command.Request.Action);
        Assert.Equal(3, command.Request.ExpectedOperationVersion);
        Assert.Equal(7, command.Request.ExpectedTargetVersion.PrimaryEntityVersion);
        Assert.Equal(5, command.Request.ExpectedTargetVersion.ProductDefinitionRevisionVersion);
        Assert.Equal("ORPHAN_RECOVERY", command.Request.ReasonCode);
        Assert.Equal("operator approved", command.Request.Comment);
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("operatorSubjectId")]
    [InlineData("successorOperationId")]
    [InlineData("workflowEvidence")]
    [InlineData("operationFingerprint")]
    [InlineData("leaseOwner")]
    [InlineData("auditFacts")]
    [InlineData("unknown")]
    [InlineData("Action")]
    public async Task Unknown_technical_or_case_alias_top_level_fields_fail_before_dispatch(string field)
    {
        var json = $$"""{"action":1,"expectedOperationVersion":0,"expectedTargetVersion":{"primaryEntityVersion":0},"reasonCode":"RECOVER","comment":null,"{{field}}":"forbidden"}""";
        await AssertRejectedBeforeDispatch(Body(json));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("revisionVersionAlias")]
    [InlineData("unknown")]
    [InlineData("PrimaryEntityVersion")]
    public async Task Unknown_or_case_alias_nested_target_version_fields_fail_before_dispatch(string field)
    {
        var json = $$"""{"action":1,"expectedOperationVersion":0,"expectedTargetVersion":{"primaryEntityVersion":0,"{{field}}":1},"reasonCode":"RECOVER","comment":null}""";
        await AssertRejectedBeforeDispatch(Body(json));
    }

    [Theory]
    [InlineData("{\"action\":1,\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0,\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    public async Task Duplicate_known_fields_fail_before_dispatch(string json) =>
        await AssertRejectedBeforeDispatch(Body(json));

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"action\":0,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":\"Abandon\",\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":null,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":null,\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":null},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":null}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\",\"comment\":7}")]
    public async Task Missing_null_wrong_type_or_invalid_enum_body_fails_before_dispatch(string json) =>
        await AssertRejectedBeforeDispatch(Body(json));

    [Theory]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":-1,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":2147483647,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":-1},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":2147483647},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0,\"productDefinitionRevisionVersion\":-1},\"reasonCode\":\"RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\" RECOVER\"}")]
    [InlineData("{\"action\":1,\"expectedOperationVersion\":0,\"expectedTargetVersion\":{\"primaryEntityVersion\":0},\"reasonCode\":\"RECOVER\",\"comment\":\"trailing \"}")]
    public async Task Invalid_version_or_non_exact_text_fails_before_dispatch(string json) =>
        await AssertRejectedBeforeDispatch(Body(json));

    [Fact]
    public async Task Overlong_or_control_character_text_fails_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Body($$"""{"action":1,"expectedOperationVersion":0,"expectedTargetVersion":{"primaryEntityVersion":0},"reasonCode":"{{new string('R', 129)}}"}"""));
        await AssertRejectedBeforeDispatch(Body($$"""{"action":1,"expectedOperationVersion":0,"expectedTargetVersion":{"primaryEntityVersion":0},"reasonCode":"RECOVER","comment":"{{new string('C', 513)}}"}"""));
        await AssertRejectedBeforeDispatch(Body("""{"action":1,"expectedOperationVersion":0,"expectedTargetVersion":{"primaryEntityVersion":0},"reasonCode":"RECOVER\u0001"}"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{f47ac10b-58cc-4372-a567-0e02b2c3d479}")]
    [InlineData(" f47ac10b-58cc-4372-a567-0e02b2c3d479")]
    [InlineData("F47AC10B-58CC-4372-A567-0E02B2C3D479")]
    public async Task Missing_empty_or_noncanonical_D_idempotency_key_fails_before_dispatch(string? value)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var controller = Controller(mediator, value);
        var result = await controller.RecoverBeforeStart(Guid.NewGuid(), ValidBody(), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((CapturingMediator)(object)mediator).Request);
    }

    [Fact]
    public async Task Duplicate_idempotency_key_fails_before_dispatch()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var controller = Controller(mediator, null);
        controller.Request.Headers["Idempotency-Key"] = new StringValues(
            [Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")]);
        var result = await controller.RecoverBeforeStart(Guid.NewGuid(), ValidBody(), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((CapturingMediator)(object)mediator).Request);
    }

    [Fact]
    public async Task Empty_route_identity_fails_before_dispatch()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var controller = Controller(mediator, Guid.NewGuid().ToString("D"));
        var result = await controller.RecoverBeforeStart(Guid.Empty, ValidBody(), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((CapturingMediator)(object)mediator).Request);
    }

    [Theory]
    [InlineData("{f47ac10b-58cc-4372-a567-0e02b2c3d479}")]
    [InlineData("F47AC10B-58CC-4372-A567-0E02B2C3D479")]
    [InlineData(" f47ac10b-58cc-4372-a567-0e02b2c3d479")]
    public async Task Noncanonical_raw_route_identity_fails_before_dispatch(string rawOperationId)
    {
        var operationId = Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var controller = Controller(mediator, Guid.NewGuid().ToString("D"));
        controller.Request.RouteValues["operationId"] = rawOperationId;

        var result = await controller.RecoverBeforeStart(operationId, ValidBody(), default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((CapturingMediator)(object)mediator).Request);
    }

    [Fact]
    public async Task Successful_response_exposes_only_sanitized_terminal_and_successor_contract()
    {
        var operationId = Guid.NewGuid();
        var successorId = Guid.NewGuid();
        var response = Response<ProductIdentityWorkflowOperationRecoveryResult>.Success(
            new ProductIdentityWorkflowOperationRecoveryResult(
                operationId,
                ProductIdentityWorkflowOperationFamily.GlobalProduct,
                ProductIdentityWorkflowRecoveryDisposition.Superseded,
                4,
                new ProductIdentityWorkflowTargetVersions(8),
                new ProductIdentityWorkflowOperationRecoverySuccessorResult(
                    successorId,
                    new ProductIdentityWorkflowTargetVersions(9))),
            200);
        var mediator = DispatchProxy.Create<IMediator, ResponseMediator>();
        ((ResponseMediator)(object)mediator).Response = response;
        var controller = Controller(mediator, Guid.NewGuid().ToString("D"));

        var result = await controller.RecoverBeforeStart(operationId, ValidBody(), default);

        var envelope = Assert.IsType<Response<ProductIdentityWorkflowOperationRecoveryResult>>(
            Assert.IsType<OkObjectResult>(result).Value);
        var serialized = JsonSerializer.Serialize(envelope);
        Assert.Contains(operationId.ToString("D"), serialized, StringComparison.Ordinal);
        Assert.Contains(successorId.ToString("D"), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("maker", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workflow", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lease", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotency", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertRejectedBeforeDispatch(JsonElement body)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var controller = Controller(mediator, Guid.NewGuid().ToString("D"));
        var result = await controller.RecoverBeforeStart(Guid.NewGuid(), body, default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((CapturingMediator)(object)mediator).Request);
    }

    internal static JsonElement ValidBody() => Body("""{"action":1,"expectedOperationVersion":0,"expectedTargetVersion":{"primaryEntityVersion":0,"productDefinitionRevisionVersion":null},"reasonCode":"RECOVER","comment":null}""");

    internal static JsonElement Body(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    internal static ProductIdentityWorkflowOperationsController Controller(
        IMediator mediator,
        string? idempotencyKey,
        IEnumerable<Claim>? claims = null,
        Guid? resolvedTenantId = null,
        bool resolveTenant = true)
    {
        var effectiveClaims = claims?.ToArray() ??
        [
            new Claim("actor_type", "tenant_user"),
            new Claim("sub", SubjectId.ToString("D")),
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("permission", RecoverPermission)
        ];
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(effectiveClaims, "test"))
        };
        if (idempotencyKey is not null) context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        var tenantContext = new TenantContext();
        if (resolveTenant)
        {
            tenantContext.SetTenant(resolvedTenantId ?? TenantId);
        }
        return new ProductIdentityWorkflowOperationsController(mediator, tenantContext)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    internal sealed class CapturedRequestException : Exception;

    internal class CapturingMediator : DispatchProxy
    {
        public object? Request { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Request = args?.FirstOrDefault();
            throw new CapturedRequestException();
        }
    }

    internal class ResponseMediator : DispatchProxy
    {
        public object? Response { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(Response);
            var responseType = targetMethod.ReturnType.GetGenericArguments().Single();
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(responseType)
                .Invoke(null, [Response]);
        }
    }
}
