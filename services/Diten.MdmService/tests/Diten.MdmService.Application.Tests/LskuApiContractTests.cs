using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuApiContractTests
{
    [Fact]
    public void Controller_exposes_the_four_foundation_and_four_lifecycle_routes()
    {
        var type = typeof(LskusController);
        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.Equal("api/lskus", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => (method.Name, Http: Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>())))
            .ToArray();

        Assert.Equal(8, routes.Length);
        Assert.Contains(routes, x => x.Name == nameof(LskusController.GetAll) && x.Http.Template is null && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.GetById) && x.Http.Template == "{id:guid}" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.GetCreateOptions) && x.Http.Template == "create-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.CreateDraft) && x.Http.Template == "drafts" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.SubmitIdentity) && x.Http.Template == "{id:guid}/submit" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.WithdrawIdentityApproval) && x.Http.Template == "{id:guid}/identity-approval/withdraw" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.RequestRetirement) && x.Http.Template == "{id:guid}/retirement-requests" && x.Http.HttpMethods.Single() == "POST");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.RetireIdentity) && x.Http.Template == "{id:guid}/retire" && x.Http.HttpMethods.Single() == "POST");
        Assert.DoesNotContain(routes, x => x.Http.HttpMethods.Any(verb => verb is "PUT" or "PATCH" or "DELETE"));
        Assert.DoesNotContain(routes, x => (x.Http.Template ?? string.Empty).Contains("reservation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, x => (x.Http.Template ?? string.Empty).Contains("approve", StringComparison.OrdinalIgnoreCase)
            || (x.Http.Template ?? string.Empty).Contains("reject", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("idempotencyKey")]
    [InlineData("canonicalCode")]
    [InlineData("marketSelection")]
    [InlineData("legalEntityId")]
    [InlineData("finishedGoodId")]
    [InlineData("unknownField")]
    public async Task Unknown_or_forbidden_json_is_rejected_before_the_handler(string field)
    {
        var json = $$"""{"gskuId":"{{Guid.NewGuid()}}","marketCode":"TR","{{field}}":"x"}""";
        var body = JsonSerializer.Deserialize<LskusController.CreateLskuDraftPublicRequest>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotNull(body.UnmappedFields);

        var mediator = DispatchProxy.Create<IMediator, ThrowingMediator>();
        var recorder = (ThrowingMediator)(object)mediator;
        var result = await new LskusController(mediator).CreateDraft(body, "trusted-header", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(recorder.Called);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Missing_or_invalid_idempotency_header_is_rejected_before_the_handler(string? header)
    {
        var mediator = DispatchProxy.Create<IMediator, ThrowingMediator>();
        var recorder = (ThrowingMediator)(object)mediator;
        var result = await new LskusController(mediator).CreateDraft(
            new LskusController.CreateLskuDraftPublicRequest { GskuId = Guid.NewGuid(), MarketCode = "TR" },
            header,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(recorder.Called);
    }

    [Fact]
    public void Public_create_contract_has_exactly_two_fields_and_sanitized_result_has_no_technical_evidence()
    {
        var requestJson = JsonSerializer.SerializeToElement(new LskusController.CreateLskuDraftPublicRequest
        {
            GskuId = Guid.NewGuid(), MarketCode = "TR"
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(["gskuId", "marketCode"], requestJson.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));

        var responseJson = JsonSerializer.Serialize(new LskusController.LskuDraftPublicResponse(
            Guid.NewGuid(), "LS-1", Guid.NewGuid(), "GS-1", "TR",
            Diten.MdmService.Domain.Enums.ProductIdentityLifecycleStatus.Draft, 0));
        foreach (var forbidden in new[] { "marketSelection", "catalog", "reservation", "binding", "credential", "referenceTenant", "command" })
        {
            Assert.DoesNotContain(forbidden, responseJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Lifecycle_routes_dispatch_exact_commands_and_preserve_reason_and_comment()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
        var capture = (CapturingMediator)(object)mediator;
        var controller = new LskusController(mediator);
        var id = Guid.NewGuid();
        var operationId = Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");

        await CaptureAsync(
            () => controller.SubmitIdentity(id, new() { ExpectedVersion = 3 }, operationId.ToString("D").ToUpperInvariant(), default),
            capture,
            request =>
            {
                var command = Assert.IsType<StartLskuIdentityWorkflowCommand>(request);
                Assert.Equal(id, command.Request.LskuId);
                Assert.Equal(3, command.Request.ExpectedVersion);
                Assert.Equal(operationId, command.Request.OperationId);
            });
        await CaptureAsync(
            () => controller.WithdrawIdentityApproval(id, new()
            {
                ExpectedVersion = 4,
                ReasonCode = "REQUESTER_WITHDRAWAL",
                Comment = "changed"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<WithdrawLskuIdentityApprovalCommand>(request);
                Assert.Equal(id, command.Request.LskuId);
                Assert.Equal(4, command.Request.ExpectedVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal("REQUESTER_WITHDRAWAL", command.Request.ReasonCode);
                Assert.Equal("changed", command.Request.Comment);
            });
        await CaptureAsync(
            () => controller.RequestRetirement(id, new()
            {
                ExpectedVersion = 5,
                RequestReason = "NO_LONGER_MARKETED"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<StartLskuRetirementRequestWorkflowCommand>(request);
                Assert.Equal(id, command.Request.LskuId);
                Assert.Equal(5, command.Request.ExpectedVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal("NO_LONGER_MARKETED", command.Request.RequestReason);
            });
        await CaptureAsync(
            () => controller.RetireIdentity(id, new()
            {
                ExpectedVersion = 5,
                ReasonCode = "IDENTITY_RETIRED",
                Comment = "obsolete"
            }, operationId.ToString("D"), default),
            capture,
            request =>
            {
                var command = Assert.IsType<RetireLskuIdentityCommand>(request);
                Assert.Equal(id, command.Request.LskuId);
                Assert.Equal(5, command.Request.ExpectedVersion);
                Assert.Equal(operationId, command.Request.OperationId);
                Assert.Equal("IDENTITY_RETIRED", command.Request.ReasonCode);
                Assert.Equal("obsolete", command.Request.Comment);
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{f47ac10b-58cc-4372-a567-0e02b2c3d479}")]
    [InlineData(" f47ac10b-58cc-4372-a567-0e02b2c3d479")]
    [InlineData("f47ac10b-58cc-4372-a567-0e02b2c3d479 ")]
    [InlineData("f47ac10b-58cc-4372-a567-0e02b2c3d479x")]
    public async Task Lifecycle_missing_non_D_or_empty_idempotency_key_fails_before_dispatch(string? key)
    {
        var mediator = DispatchProxy.Create<IMediator, ThrowingMediator>();
        var recorder = (ThrowingMediator)(object)mediator;
        var controller = new LskusController(mediator);

        var submit = await controller.SubmitIdentity(Guid.NewGuid(), new() { ExpectedVersion = 0 }, key, default);
        var retire = await controller.RetireIdentity(Guid.NewGuid(), new()
        {
            ExpectedVersion = 0,
            ReasonCode = "IDENTITY_RETIRED"
        }, key, default);
        var withdraw = await controller.WithdrawIdentityApproval(Guid.NewGuid(), new()
        {
            ExpectedVersion = 1,
            ReasonCode = "REQUESTER_WITHDRAWAL"
        }, key, default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(submit).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(retire).StatusCode);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(withdraw).StatusCode);
        Assert.False(recorder.Called);
    }

    [Fact]
    public async Task Lifecycle_missing_version_or_unknown_field_fails_before_dispatch()
    {
        var mediator = DispatchProxy.Create<IMediator, ThrowingMediator>();
        var recorder = (ThrowingMediator)(object)mediator;
        var controller = new LskusController(mediator);
        var unknown = new Dictionary<string, JsonElement>
        {
            ["tenantId"] = JsonSerializer.SerializeToElement("forbidden")
        };
        var key = Guid.NewGuid().ToString("D");

        var missingSubmit = await controller.SubmitIdentity(Guid.NewGuid(), new(), key, default);
        var missingRetire = await controller.RetireIdentity(Guid.NewGuid(), new()
        {
            ReasonCode = "IDENTITY_RETIRED"
        }, key, default);
        var unknownSubmit = await controller.SubmitIdentity(Guid.NewGuid(), new()
        {
            ExpectedVersion = 0,
            UnmappedFields = unknown
        }, key, default);

        Assert.All([missingSubmit, missingRetire, unknownSubmit], result =>
            Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode));
        Assert.False(recorder.Called);
    }

    [Fact]
    public void Lifecycle_bodies_are_strict_and_do_not_accept_actor_tenant_or_operation_fields()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var submit = JsonSerializer.SerializeToElement(
            new LskusController.SubmitLskuIdentityApiRequest { ExpectedVersion = 2 }, options);
        var retire = JsonSerializer.SerializeToElement(
            new LskusController.RetireLskuIdentityApiRequest
            {
                ExpectedVersion = 4,
                ReasonCode = "IDENTITY_RETIRED",
                Comment = "obsolete"
            }, options);
        var withdraw = JsonSerializer.SerializeToElement(
            new LskusController.WithdrawLskuIdentityApprovalApiRequest
            {
                ExpectedVersion = 3,
                ReasonCode = "REQUESTER_WITHDRAWAL",
                Comment = "changed"
            }, options);
        var forbidden = JsonSerializer.Deserialize<LskusController.SubmitLskuIdentityApiRequest>(
            "{\"expectedVersion\":2,\"actorId\":\"forbidden\"}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(["expectedVersion"], submit.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedVersion", "reasonCode", "comment"], retire.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["expectedVersion", "reasonCode", "comment"], withdraw.EnumerateObject().Select(x => x.Name));
        Assert.Equal(["actorId"], forbidden.UnmappedFields!.Keys);
        Assert.DoesNotContain("tenant", retire.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operation", retire.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CaptureAsync(
        Func<Task<IActionResult>> action,
        CapturingMediator capture,
        Action<object> assertion)
    {
        capture.Request = null;
        await Assert.ThrowsAsync<CapturedRequestException>(action);
        assertion(Assert.IsAssignableFrom<object>(capture.Request));
    }

    private sealed class CapturedRequestException : Exception;

    private class CapturingMediator : DispatchProxy
    {
        public object? Request { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Request = args?.FirstOrDefault();
            throw new CapturedRequestException();
        }
    }

    private class ThrowingMediator : DispatchProxy
    {
        public bool Called { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Called = true;
            throw new InvalidOperationException("The mediator must not be called for rejected transport input.");
        }
    }
}
