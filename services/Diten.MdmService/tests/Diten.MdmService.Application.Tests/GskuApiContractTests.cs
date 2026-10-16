using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Options_routes_bind_target_and_operation_without_client_permission(bool correction)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new GskusController(mediator);
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<CapturedRequestException>(() => correction
            ? controller.GetCorrectionOptions(id, default) : controller.GetEditOptions(id, default));
        var query = Assert.IsType<GetGskuMutationOptionsQuery>(capture.Request);
        Assert.Equal(id, query.GskuId);
        Assert.Equal(correction ? GskuMutationOptionsOperation.Correction : GskuMutationOptionsOperation.Edit, query.Operation);
        Assert.DoesNotContain(typeof(GetGskuMutationOptionsQuery).GetProperties(), x => x.Name.Contains("Permission"));
    }

    [Fact]
    public void Lifecycle_filter_rejects_undefined_enum_values()
    {
        var validator = new GetGskusValidator();
        Assert.True(validator.Validate(new GetGskusQuery
            { LifecycleStatus = ProductIdentityLifecycleStatus.Draft }).IsValid);
        Assert.False(validator.Validate(new GetGskusQuery
            { LifecycleStatus = (ProductIdentityLifecycleStatus)999 }).IsValid);
    }

    [Fact]
    public void Controller_exposes_the_green_foundation_and_lifecycle_routes()
    {
        var type = typeof(GskusController);
        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.Equal("api/gskus", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => (method.Name, Http: Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>())))
            .ToArray();
        Assert.Equal(12, routes.Length);
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetEditOptions)
            && x.Http.Template == "{id:guid}/edit-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetCorrectionOptions)
            && x.Http.Template == "{id:guid}/correction-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetAll) && x.Http.Template is null && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetById) && x.Http.Template == "{id:guid}" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetCreateOptions) && x.Http.Template == "create-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.CreateDraft) && x.Http.Template == "drafts" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.UpdateDraft) && x.Http.Template == "{id:guid}" && x.Http.HttpMethods.Single() == "PUT");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.SubmitIdentity) && x.Http.Template == "{id:guid}/submit" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.WithdrawIdentityApproval) && x.Http.Template == "{id:guid}/identity-approval/withdraw" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.RequestCorrection) && x.Http.Template == "{id:guid}/correction-requests" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.RequestRetirement) && x.Http.Template == "{id:guid}/retirement-requests" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.RetireIdentity) && x.Http.Template == "{id:guid}/retire" && x.Http.HttpMethods.Single() == "POST");
        Assert.DoesNotContain(routes, x => x.Http.HttpMethods.Any(v => v is "PATCH" or "DELETE"));
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
            () => controller.UpdateDraft(gskuId, new()
            {
                ExpectedVersion = 2, PackQuantity = 3.5m, PackUomCode = "C62"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<UpdateGskuDraftCommand>(request);
                Assert.Equal(gskuId, command.Request.GskuId);
                Assert.Equal(2, command.Request.ExpectedVersion);
                Assert.Equal(3.5m, command.Request.PackQuantity);
                Assert.Equal("C62", command.Request.PackUomCode);
                Assert.Equal(operationId, command.OperationId);
            });

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
            () => controller.WithdrawIdentityApproval(gskuId, new()
            {
                ExpectedGskuVersion = 4, ReasonCode = "REQUESTER_WITHDRAWAL", Comment = "Changed"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<WithdrawFirstGskuIdentityApprovalCommand>(request);
                Assert.Equal(gskuId, command.Request.GskuId);
                Assert.Equal(4, command.Request.ExpectedGskuVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal("REQUESTER_WITHDRAWAL", command.Request.ReasonCode);
                Assert.Equal("Changed", command.Request.Comment);
            });
        await CaptureAsync(
            () => controller.RequestCorrection(gskuId, new()
            {
                ExpectedGskuVersion = 5, PackQuantity = 12.5m, PackUomCode = "KGM"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<StartGskuCorrectionWorkflowCommand>(request);
                Assert.Equal(gskuId, command.Request.GskuId);
                Assert.Equal(5, command.Request.ExpectedGskuVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal(12.5m, command.Request.PackQuantity);
                Assert.Equal("KGM", command.Request.PackUomCode);
            });
        await CaptureAsync(
            () => controller.RequestRetirement(gskuId, new()
            {
                ExpectedGskuVersion = 6, RequestReason = "obsolete"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<StartGskuRetirementRequestWorkflowCommand>(request);
                Assert.Equal(gskuId, command.Request.GskuId);
                Assert.Equal(6, command.Request.ExpectedGskuVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal("obsolete", command.Request.RequestReason);
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
        var update = await controller.UpdateDraft(
            Guid.NewGuid(), new() { ExpectedVersion = 0, PackQuantity = 1, PackUomCode = "C62", UnmappedFields = unknown },
            hasUnknownField ? Guid.NewGuid().ToString("D") : idempotencyKey, default);
        var withdraw = await controller.WithdrawIdentityApproval(
            Guid.NewGuid(), new() { ExpectedGskuVersion = 1, ReasonCode = "REQUESTER_WITHDRAWAL", UnmappedFields = unknown },
            hasUnknownField ? Guid.NewGuid().ToString("D") : idempotencyKey, default);
        var correction = await controller.RequestCorrection(
            Guid.NewGuid(), new()
            {
                ExpectedGskuVersion = 1, PackQuantity = 1, PackUomCode = "C62", UnmappedFields = unknown
            }, hasUnknownField ? Guid.NewGuid().ToString("D") : idempotencyKey, default);
        var retirementRequest = await controller.RequestRetirement(
            Guid.NewGuid(), new()
            {
                ExpectedGskuVersion = 1, RequestReason = "obsolete", UnmappedFields = unknown
            }, hasUnknownField ? Guid.NewGuid().ToString("D") : idempotencyKey, default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(submit).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retire).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(update).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(withdraw).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(correction).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retirementRequest).StatusCode);
        Assert.Null(capture.Request);
    }

    [Fact]
    public void Lifecycle_public_bodies_contain_only_exact_business_fields()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var submit = JsonSerializer.SerializeToElement(
            new GskusController.SubmitGskuIdentityApiRequest { ExpectedVersion = 2 }, options);
        var retire = JsonSerializer.SerializeToElement(
            new GskusController.RetireGskuIdentityApiRequest { ExpectedVersion = 4, ReasonCode = "IDENTITY_RETIRED" }, options);
        var update = JsonSerializer.SerializeToElement(new GskusController.UpdateGskuDraftApiRequest
            { ExpectedVersion = 3, PackQuantity = 2, PackUomCode = "C62" }, options);
        var withdraw = JsonSerializer.SerializeToElement(new GskusController.WithdrawGskuIdentityApprovalApiRequest
            { ExpectedGskuVersion = 4, ReasonCode = "REQUESTER_WITHDRAWAL", Comment = "Changed" }, options);
        var correction = JsonSerializer.SerializeToElement(new GskusController.RequestGskuCorrectionApiRequest
            { ExpectedGskuVersion = 5, PackQuantity = 2, PackUomCode = "KGM" }, options);
        var retirementRequest = JsonSerializer.SerializeToElement(new GskusController.RequestGskuRetirementApiRequest
            { ExpectedGskuVersion = 6, RequestReason = "obsolete" }, options);
        var forbidden = JsonSerializer.Deserialize<GskusController.SubmitGskuIdentityApiRequest>(
            "{\"expectedVersion\":2,\"actorId\":\"forbidden\"}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(["expectedVersion"], submit.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedVersion", "reasonCode"], retire.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedVersion", "packQuantity", "packUomCode"], update.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedGskuVersion", "reasonCode", "comment"], withdraw.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedGskuVersion", "packQuantity", "packUomCode"], correction.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedGskuVersion", "requestReason"], retirementRequest.EnumerateObject().Select(x => x.Name));
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
        var updateBody = JsonSerializer.Deserialize<GskusController.UpdateGskuDraftApiRequest>(
            "{\"packQuantity\":1,\"packUomCode\":\"C62\"}", options)!;
        var withdrawBody = JsonSerializer.Deserialize<GskusController.WithdrawGskuIdentityApprovalApiRequest>(
            "{\"reasonCode\":\"REQUESTER_WITHDRAWAL\"}", options)!;
        var correctionBody = JsonSerializer.Deserialize<GskusController.RequestGskuCorrectionApiRequest>(
            "{\"packQuantity\":1,\"packUomCode\":\"C62\"}", options)!;
        var retirementRequestBody = JsonSerializer.Deserialize<GskusController.RequestGskuRetirementApiRequest>(
            "{\"requestReason\":\"obsolete\"}", options)!;
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new GskusController(mediator);
        var operationId = Guid.NewGuid().ToString("D");

        var submit = await controller.SubmitIdentity(Guid.NewGuid(), submitBody, operationId, default);
        var retire = await controller.RetireIdentity(Guid.NewGuid(), retireBody, operationId, default);
        var update = await controller.UpdateDraft(Guid.NewGuid(), updateBody, operationId, default);
        var withdraw = await controller.WithdrawIdentityApproval(Guid.NewGuid(), withdrawBody, operationId, default);
        var correction = await controller.RequestCorrection(Guid.NewGuid(), correctionBody, operationId, default);
        var retirementRequest = await controller.RequestRetirement(
            Guid.NewGuid(), retirementRequestBody, operationId, default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(submit).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retire).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(update).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(withdraw).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(correction).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retirementRequest).StatusCode);
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
