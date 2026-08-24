using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuApiContractTests
{
    [Fact]
    public void Controller_exposes_exactly_the_four_frozen_routes()
    {
        var type = typeof(LskusController);
        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.Equal("api/lskus", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => (method.Name, Http: Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>())))
            .ToArray();

        Assert.Equal(4, routes.Length);
        Assert.Contains(routes, x => x.Name == nameof(LskusController.GetAll) && x.Http.Template is null && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.GetById) && x.Http.Template == "{id:guid}" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.GetCreateOptions) && x.Http.Template == "create-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(LskusController.CreateDraft) && x.Http.Template == "drafts" && x.Http.HttpMethods.Single() == "POST");
        Assert.DoesNotContain(routes, x => x.Http.HttpMethods.Any(verb => verb is "PUT" or "PATCH" or "DELETE"));
        Assert.DoesNotContain(routes, x => (x.Http.Template ?? string.Empty).Contains("reservation", StringComparison.OrdinalIgnoreCase));
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
