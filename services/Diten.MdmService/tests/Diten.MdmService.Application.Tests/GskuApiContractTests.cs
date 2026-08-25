using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
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
    public void Controller_exposes_exactly_the_four_frozen_routes()
    {
        var type = typeof(GskusController);
        Assert.True(type.IsSubclassOf(typeof(CustomBaseController)));
        Assert.Equal("api/gskus", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => (method.Name, Http: Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>())))
            .ToArray();
        Assert.Equal(4, routes.Length);
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetAll) && x.Http.Template is null && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetById) && x.Http.Template == "{id:guid}" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.GetCreateOptions) && x.Http.Template == "create-options" && x.Http.HttpMethods.Single() == "GET");
        Assert.Contains(routes, x => x.Name == nameof(GskusController.CreateDraft) && x.Http.Template == "drafts" && x.Http.HttpMethods.Single() == "POST");
        Assert.DoesNotContain(routes, x => x.Http.HttpMethods.Any(v => v is "PUT" or "PATCH" or "DELETE"));
        Assert.DoesNotContain(routes, x => (x.Http.Template ?? string.Empty).Contains("reservation", StringComparison.OrdinalIgnoreCase));
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
}
