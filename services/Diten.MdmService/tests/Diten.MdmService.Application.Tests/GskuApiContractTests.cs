using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuApiContractTests
{
    [Fact]
    public void Controller_exposes_the_four_foundation_and_two_lifecycle_routes()
    {
        var type = typeof(GskusController);
        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.Equal("api/gskus", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => (method.Name, Http: Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>())))
            .ToArray();
        Assert.Equal(6, routes.Length);
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetAll) && x.Http.Template is null && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetById) && x.Http.Template == "{id:guid}" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetCreateOptions) && x.Http.Template == "create-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.CreateDraft) && x.Http.Template == "drafts" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.SubmitIdentity) && x.Http.Template == "{id:guid}/submit" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.RetireIdentity) && x.Http.Template == "{id:guid}/retire" && x.Http.HttpMethods.Single() == "POST");
        Assert.DoesNotContain(routes, x => x.Http.HttpMethods.Any(v => v is "PUT" or "PATCH" or "DELETE"));
        Assert.DoesNotContain(routes, x => (x.Http.Template ?? string.Empty).Contains("reservation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, x => (x.Http.Template ?? string.Empty).Contains("approve", StringComparison.OrdinalIgnoreCase)
            || (x.Http.Template ?? string.Empty).Contains("reject", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("canonicalCode")]
    [InlineData("catalogVersionId")]
    [InlineData("referenceTenantId")]
    [InlineData("unknownField")]
    public async Task Unknown_or_forbidden_json_returns_400_before_handler(string field)
    {
        var json = $$"""{"globalProductId":"{{Guid.NewGuid()}}","packQuantity":1,"packUomCode":"C62","{{field}}":"x"}""";
        var body = JsonSerializer.Deserialize<ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var command = new CreateFirstGskuDraftFacadeCommand(body, "opaque-server-operation");
        var behavior = new ValidationBehavior<CreateFirstGskuDraftFacadeCommand,
            Response<ProductItemSkuMasterModels.GskuDraftResponse>>([new CreateFirstGskuDraftFacadeValidator()]);
        var dispatched = false;

        var response = await behavior.Handle(command, () =>
        {
            dispatched = true;
            return Task.FromResult(Response<ProductItemSkuMasterModels.GskuDraftResponse>.Fail("unexpected", 500));
        }, CancellationToken.None);

        Assert.False(dispatched);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Public_create_contract_has_three_fields_and_result_leaks_no_technical_evidence()
    {
        var requestJson = JsonSerializer.SerializeToElement(new ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest
        {
            GlobalProductId = Guid.NewGuid(), PackQuantity = 1.25m, PackUomCode = "KGM"
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(["globalProductId", "packQuantity", "packUomCode"],
            requestJson.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));

        var resultJson = JsonSerializer.Serialize(new ProductItemSkuMasterModels.GskuDraftResponse(
            Guid.NewGuid(), "GS-1", Guid.NewGuid(), Guid.NewGuid(), "REV-001", 1m, "C62",
            ProductIdentityLifecycleStatus.Draft, 0));
        Assert.DoesNotContain("reservation", resultJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("catalog", resultJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("creationCommand", resultJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lifecycle_routes_dispatch_route_id_header_operation_and_strict_body_to_exact_commands()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new GskusController(mediator);
        var gskuId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        await CaptureAsync(
            () => controller.SubmitIdentity(gskuId, new() { ExpectedVersion = 3 }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<StartFirstGskuIdentityWorkflowCommand>(request);
                Assert.Equal(gskuId, command.Request.GskuId);
                Assert.Equal(3, command.Request.ExpectedGskuVersion);
                Assert.Equal(operationId, command.Request.OperationId);
            });
        await CaptureAsync(
            () => controller.RetireIdentity(gskuId, new() { ExpectedVersion = 5, ReasonCode = "IDENTITY_RETIRED" }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<RetireGskuIdentityPairCommand>(request);
                Assert.Equal(gskuId, command.Request.GskuId);
                Assert.Equal(5, command.Request.ExpectedGskuVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal("IDENTITY_RETIRED", command.Request.ReasonCode);
            });
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(false, "not-a-guid")]
    [InlineData(false, "00000000-0000-0000-0000-000000000000")]
    [InlineData(false, "{f47ac10b-58cc-4372-a567-0e02b2c3d479}")]
    public async Task Lifecycle_unknown_fields_or_non_exact_D_idempotency_fail_before_dispatch(
        bool hasUnknownField,
        string? idempotencyKey)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new GskusController(mediator);
        var unknown = hasUnknownField
            ? new Dictionary<string, JsonElement> { ["tenantId"] = JsonSerializer.SerializeToElement("forbidden") }
            : null;

        var submit = await controller.SubmitIdentity(
            Guid.NewGuid(), new() { ExpectedVersion = 0, UnmappedFields = unknown },
            hasUnknownField ? Guid.NewGuid().ToString("D") : idempotencyKey, default);
        var retire = await controller.RetireIdentity(
            Guid.NewGuid(), new() { ExpectedVersion = 0, ReasonCode = "IDENTITY_RETIRED", UnmappedFields = unknown },
            hasUnknownField ? Guid.NewGuid().ToString("D") : idempotencyKey, default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(submit).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retire).StatusCode);
        Assert.Null(capture.Request);
    }

    [Fact]
    public void Lifecycle_public_bodies_contain_only_expected_version_and_retirement_reason()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var submit = JsonSerializer.SerializeToElement(
            new GskusController.SubmitGskuIdentityApiRequest { ExpectedVersion = 2 }, options);
        var retire = JsonSerializer.SerializeToElement(
            new GskusController.RetireGskuIdentityApiRequest { ExpectedVersion = 4, ReasonCode = "IDENTITY_RETIRED" }, options);
        var forbidden = JsonSerializer.Deserialize<GskusController.SubmitGskuIdentityApiRequest>(
            "{\"expectedVersion\":2,\"actorId\":\"forbidden\"}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(["expectedVersion"], submit.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedVersion", "reasonCode"], retire.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["actorId"], forbidden.UnmappedFields!.Keys);
        Assert.DoesNotContain("tenant", retire.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operation", retire.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lifecycle_missing_expected_version_in_raw_json_fails_before_dispatch_for_both_routes()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var submitBody = JsonSerializer.Deserialize<GskusController.SubmitGskuIdentityApiRequest>("{}", options)!;
        var retireBody = JsonSerializer.Deserialize<GskusController.RetireGskuIdentityApiRequest>(
            "{\"reasonCode\":\"IDENTITY_RETIRED\"}", options)!;
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new GskusController(mediator);
        var operationId = Guid.NewGuid().ToString("D");

        var submit = await controller.SubmitIdentity(Guid.NewGuid(), submitBody, operationId, default);
        var retire = await controller.RetireIdentity(Guid.NewGuid(), retireBody, operationId, default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(submit).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retire).StatusCode);
        Assert.Null(capture.Request);
    }

    private static async Task CaptureAsync(
        Func<Task<IActionResult>> action,
        CapturingMediatorProxy capture,
        Action<object> assertion)
    {
        capture.Request = null;
        await Assert.ThrowsAsync<CapturedRequestException>(action);
        assertion(Assert.IsAssignableFrom<object>(capture.Request));
    }

    private sealed class CapturedRequestException : Exception;

    private class CapturingMediatorProxy : DispatchProxy
    {
        public object? Request { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Request = args?.FirstOrDefault();
            throw new CapturedRequestException();
        }
    }
}
