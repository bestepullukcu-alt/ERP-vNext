using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodApiContractTests
{
    [Fact]
    public void Controller_is_an_authorized_thin_response_envelope_surface()
    {
        var type = typeof(FinishedGoodsController);

        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.Equal("api/finished-goods", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var constructor = Assert.Single(type.GetConstructors());
        Assert.Equal(typeof(IMediator), Assert.Single(constructor.GetParameters()).ParameterType);
    }

    [Theory]
    [InlineData(nameof(FinishedGoodsController.GetAll), "GET", null)]
    [InlineData(nameof(FinishedGoodsController.GetById), "GET", "{id:guid}")]
    [InlineData(nameof(FinishedGoodsController.GetGskuSelector), "GET", "gsku-selector")]
    [InlineData(nameof(FinishedGoodsController.CreateDraft), "POST", "drafts")]
    public void Endpoint_route_and_verb_are_exact(string methodName, string verb, string? route)
    {
        var method = typeof(FinishedGoodsController).GetMethod(methodName)!;
        var http = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());

        Assert.Equal([verb], http.HttpMethods);
        Assert.Equal(route, http.Template);
    }

    [Fact]
    public void Surface_has_no_reservation_update_delete_bulk_or_lifecycle_endpoint()
    {
        var actions = typeof(FinishedGoodsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var routes = actions
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .ToArray();

        Assert.Equal(4, actions.Length);
        Assert.Equal(4, routes.Length);
        Assert.DoesNotContain(routes, route => route.HttpMethods.Any(method =>
            method is "PUT" or "PATCH" or "DELETE"));
        Assert.DoesNotContain(routes, route =>
            (route.Template ?? string.Empty).Contains("reservation", StringComparison.OrdinalIgnoreCase)
            || (route.Template ?? string.Empty).Contains("bulk", StringComparison.OrdinalIgnoreCase)
            || (route.Template ?? string.Empty).Contains("lifecycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Every_action_dispatches_the_exact_existing_CQRS_request()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new FinishedGoodsController(mediator);
        var id = Guid.NewGuid();
        var create = new ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest
        {
            GskuId = Guid.NewGuid(),
            IdempotencyKey = "technical-key"
        };

        await CaptureAsync(() => controller.GetAll(new(), default), capture,
            request => Assert.IsType<GetFinishedGoodsQuery>(request));
        await CaptureAsync(() => controller.GetById(id, default), capture,
            request => Assert.Equal(id, Assert.IsType<GetFinishedGoodByIdQuery>(request).Id));
        await CaptureAsync(() => controller.GetGskuSelector(new(), default), capture,
            request => Assert.IsType<GetFinishedGoodGskuSelectorQuery>(request));
        await CaptureAsync(() => controller.CreateDraft(create, default), capture,
            request => Assert.Same(create, Assert.IsType<CreateFinishedGoodDraftCommand>(request).Request));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("canonicalCode")]
    [InlineData("codeReservationId")]
    [InlineData("lskuId")]
    [InlineData("unknownField")]
    public async Task Unknown_or_forbidden_json_is_a_400_before_handler_dispatch(string field)
    {
        var json = $$"""
            {"gskuId":"{{Guid.NewGuid()}}","idempotencyKey":"technical-key","{{field}}":"forbidden"}
            """;
        var request = JsonSerializer.Deserialize<ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var command = new CreateFinishedGoodDraftCommand(request);
        var behavior = new ValidationBehavior<
            CreateFinishedGoodDraftCommand,
            Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>>(
            [new CreateFinishedGoodDraftValidator()]);
        var handlerDispatched = false;

        var response = await behavior.Handle(
            command,
            () =>
            {
                handlerDispatched = true;
                return Task.FromResult(Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail("UNEXPECTED", 500));
            },
            CancellationToken.None);

        Assert.False(handlerDispatched);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(response.Errors, error =>
            error is "FINISHED_GOOD_FIELD_FORBIDDEN" or "UNKNOWN_WRITE_FIELD_FORBIDDEN");
    }

    [Fact]
    public void Json_projection_has_one_gsku_code_name_and_no_display_aliases()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var listJson = JsonSerializer.SerializeToElement(new ProductItemSkuMasterModels.FinishedGoodListItemDto(
            Guid.NewGuid(),
            "FG-000000000001",
            Guid.NewGuid(),
            "GS-000000000001",
            "GS-000000000001",
            ProductIdentityLifecycleStatus.Draft,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow), options);
        var selectorJson = JsonSerializer.SerializeToElement(new ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto(
            Guid.NewGuid(),
            "GS-000000000001",
            "GS-000000000001"), options);

        Assert.True(listJson.TryGetProperty("gskuCanonicalCode", out _));
        Assert.False(listJson.TryGetProperty("gskuDisplay", out _));
        Assert.False(listJson.TryGetProperty("display", out _));
        Assert.True(selectorJson.TryGetProperty("gskuCanonicalCode", out _));
        Assert.False(selectorJson.TryGetProperty("canonicalCode", out _));
        Assert.False(selectorJson.TryGetProperty("display", out _));
    }

    [Theory]
    [InlineData(201, true, null)]
    [InlineData(202, false, "FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED")]
    [InlineData(409, false, "IDEMPOTENCY_KEY_CONFLICT")]
    [InlineData(404, false, "GSKU_NOT_REFERENCEABLE")]
    public async Task Create_preserves_the_existing_response_envelope_status_contract(
        int statusCode,
        bool successful,
        string? errorCode)
    {
        var dto = new ProductItemSkuMasterModels.FinishedGoodDraftDto(
            Guid.NewGuid(),
            "FG-000000000001",
            Guid.NewGuid(),
            "GS-000000000001",
            ProductIdentityLifecycleStatus.Draft,
            0,
            CodeReservationBindingState.Confirmed,
            false);
        var response = successful
            ? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(dto, statusCode)
            : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(errorCode!, statusCode);
        var mediator = DispatchProxy.Create<IMediator, ResponseMediatorProxy>();
        ((ResponseMediatorProxy)(object)mediator).Response = response;
        var controller = new FinishedGoodsController(mediator);

        var result = await controller.CreateDraft(new()
        {
            GskuId = dto.GskuId,
            IdempotencyKey = "status-contract"
        }, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        Assert.Same(response, objectResult.Value);
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

    private class ResponseMediatorProxy : DispatchProxy
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
