using System.Reflection;
using Diten.MdmService.Api.Contracts.ProductAbbreviations;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationApiContractTests
{
    [Fact]
    public void Controller_is_authorized_thin_response_envelope_surface()
    {
        var type = typeof(ProductAbbreviationsController);

        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/product-abbreviations", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Single(type.GetConstructors().Single().GetParameters());
        Assert.Equal(typeof(IMediator), type.GetConstructors().Single().GetParameters().Single().ParameterType);
    }

    [Theory]
    [InlineData(nameof(ProductAbbreviationsController.GetByGlobalProduct), "GET", "by-global-product/{globalProductId:guid}", "mdm.product-abbreviations.read")]
    [InlineData(nameof(ProductAbbreviationsController.Resolve), "GET", "resolve/{abbreviation}", "mdm.product-abbreviations.read")]
    [InlineData(nameof(ProductAbbreviationsController.GetEvidence), "GET", "{registerEntryId:guid}/evidence", "mdm.product-abbreviations.audit")]
    [InlineData(nameof(ProductAbbreviationsController.RequestAllocation), "POST", "requests", "mdm.product-abbreviations.request")]
    [InlineData(nameof(ProductAbbreviationsController.Cancel), "PATCH", "{registerEntryId:guid}/cancel", "mdm.product-abbreviations.cancel")]
    [InlineData(nameof(ProductAbbreviationsController.Approve), "PATCH", "{registerEntryId:guid}/approve", "mdm.product-abbreviations.approve")]
    [InlineData(nameof(ProductAbbreviationsController.Reject), "PATCH", "{registerEntryId:guid}/reject", "mdm.product-abbreviations.reject")]
    [InlineData(nameof(ProductAbbreviationsController.InitiateCorrection), "POST", "{registerEntryId:guid}/corrections", "mdm.product-abbreviations.correct")]
    [InlineData(nameof(ProductAbbreviationsController.RequestRetirement), "POST", "{registerEntryId:guid}/retirement-requests", "mdm.product-abbreviations.retire")]
    [InlineData(nameof(ProductAbbreviationsController.ApproveRetirement), "PATCH", "{registerEntryId:guid}/retirement-requests/{retirementRequestId}/approve", "mdm.product-abbreviations.approve")]
    [InlineData(nameof(ProductAbbreviationsController.RejectRetirement), "PATCH", "{registerEntryId:guid}/retirement-requests/{retirementRequestId}/reject", "mdm.product-abbreviations.reject")]
    public void Endpoint_route_verb_and_permission_are_exact(
        string methodName,
        string verb,
        string route,
        string permission)
    {
        var method = typeof(ProductAbbreviationsController).GetMethod(methodName)!;
        var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        var authorization = method.GetCustomAttribute<HasPermissionAttribute>()!;

        Assert.Equal([verb], http.HttpMethods);
        Assert.Equal(route, http.Template);
        Assert.Equal($"Permission:{permission}", authorization.Policy);
    }

    [Fact]
    public void Write_transport_contracts_expose_no_trusted_or_technical_context()
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TenantId", "LegalEntityId", "CanonicalHumanSubjectId", "ActorType", "PermissionKeys",
            "LifecycleStatus", "AllocationLedgerId", "History", "CorrelationId", "IdempotencyKey"
        };
        var requestTypes = new[]
        {
            typeof(RequestAllocationRequest), typeof(CancelAllocationRequest),
            typeof(ApproveAllocationRequest), typeof(RejectAllocationRequest),
            typeof(InitiateCorrectionRequest), typeof(RequestRetirementRequest),
            typeof(ApproveRetirementRequest), typeof(RejectRetirementRequest)
        };

        Assert.All(requestTypes, requestType =>
            Assert.DoesNotContain(requestType.GetProperties(), property => forbidden.Contains(property.Name)));
        Assert.Equal(
            ["Abbreviation", "GlobalProductId"],
            typeof(RequestAllocationRequest).GetProperties().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task Every_action_dispatches_the_exact_existing_CQRS_request()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new ProductAbbreviationsController(mediator);
        var entryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await CaptureAsync(() => controller.GetByGlobalProduct(productId, default), capture,
            request => Assert.Equal(productId, Assert.IsType<GetProductAbbreviationByGlobalProductQuery>(request).GlobalProductId));
        await CaptureAsync(() => controller.Resolve("ABC", default), capture,
            request => Assert.Equal("ABC", Assert.IsType<ResolveProductAbbreviationQuery>(request).Abbreviation));
        await CaptureAsync(() => controller.GetEvidence(entryId, default), capture,
            request => Assert.Equal(entryId, Assert.IsType<GetProductAbbreviationAllocationEvidenceQuery>(request).RegisterEntryId));
        await CaptureAsync(() => controller.RequestAllocation(new(productId, "ABC"), "request-key", default), capture,
            request => Assert.Equal((productId, "ABC", "request-key"), Values(Assert.IsType<RequestProductAbbreviationAllocationCommand>(request))));
        await CaptureAsync(() => controller.Cancel(entryId, new(3, "reason"), "cancel-key", default), capture,
            request => Assert.Equal((entryId, 3, "cancel-key", "reason"), Values(Assert.IsType<CancelProductAbbreviationAllocationCommand>(request))));
        await CaptureAsync(() => controller.Approve(entryId, new(4, 2, "reason"), "approve-key", default), capture,
            request => Assert.Equal((entryId, 4, "approve-key", 2, "reason"), Values(Assert.IsType<ApproveProductAbbreviationAllocationCommand>(request))));
        await CaptureAsync(() => controller.Reject(entryId, new(5, "reason"), "reject-key", default), capture,
            request => Assert.Equal((entryId, 5, "reject-key", "reason"), Values(Assert.IsType<RejectProductAbbreviationAllocationCommand>(request))));
        await CaptureAsync(() => controller.InitiateCorrection(entryId, new(6, "XYZ", "reason"), "correct-key", default), capture,
            request => Assert.Equal((entryId, 6, "XYZ", "correct-key", "reason"), Values(Assert.IsType<InitiateProductAbbreviationCorrectionCommand>(request))));
        await CaptureAsync(() => controller.RequestRetirement(entryId, new(7, "reason"), "retire-key", default), capture,
            request => Assert.Equal((entryId, 7, "retire-key", "reason"), Values(Assert.IsType<RequestProductAbbreviationRetirementCommand>(request))));
        await CaptureAsync(() => controller.ApproveRetirement(entryId, "retirement", new(8, "reason"), "approve-retire-key", default), capture,
            request => Assert.Equal((entryId, 8, "retirement", "approve-retire-key", "reason"), Values(Assert.IsType<ApproveProductAbbreviationRetirementCommand>(request))));
        await CaptureAsync(() => controller.RejectRetirement(entryId, "retirement", new(9, "reason"), "reject-retire-key", default), capture,
            request => Assert.Equal((entryId, 9, "retirement", "reject-retire-key", "reason"), Values(Assert.IsType<RejectProductAbbreviationRetirementCommand>(request))));
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

    private static (Guid, string, string) Values(RequestProductAbbreviationAllocationCommand value)
        => (value.GlobalProductId, value.Abbreviation, value.IdempotencyKey);
    private static (Guid, int, string, string?) Values(CancelProductAbbreviationAllocationCommand value)
        => (value.RegisterEntryId, value.ExpectedVersion, value.IdempotencyKey, value.Reason);
    private static (Guid, int, string, int?, string?) Values(ApproveProductAbbreviationAllocationCommand value)
        => (value.RegisterEntryId, value.ExpectedVersion, value.IdempotencyKey, value.ExpectedFormerVersion, value.Reason);
    private static (Guid, int, string, string) Values(RejectProductAbbreviationAllocationCommand value)
        => (value.RegisterEntryId, value.ExpectedVersion, value.IdempotencyKey, value.Reason);
    private static (Guid, int, string, string, string) Values(InitiateProductAbbreviationCorrectionCommand value)
        => (value.ActiveRegisterEntryId, value.ExpectedVersion, value.ReplacementAbbreviation, value.IdempotencyKey, value.Reason);
    private static (Guid, int, string, string) Values(RequestProductAbbreviationRetirementCommand value)
        => (value.RegisterEntryId, value.ExpectedVersion, value.IdempotencyKey, value.Reason);
    private static (Guid, int, string, string, string?) Values(ApproveProductAbbreviationRetirementCommand value)
        => (value.RegisterEntryId, value.ExpectedVersion, value.RetirementRequestId, value.IdempotencyKey, value.Reason);
    private static (Guid, int, string, string, string) Values(RejectProductAbbreviationRetirementCommand value)
        => (value.RegisterEntryId, value.ExpectedVersion, value.RetirementRequestId, value.IdempotencyKey, value.Reason);

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
