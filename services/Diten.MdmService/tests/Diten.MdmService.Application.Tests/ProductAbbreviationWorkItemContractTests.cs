using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Contracts.ProductAbbreviationWorkItems;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemContractTests
{
    [Fact]
    public void Controller_exposes_only_the_exact_remote_provider_pair()
    {
        var type = typeof(ProductAbbreviationWorkItemsController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/work-items/product-abbreviations", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal(typeof(IMediator), Assert.Single(type.GetConstructors()).GetParameters().Single().ParameterType);

        var projection = type.GetMethod(nameof(ProductAbbreviationWorkItemsController.GetProjection))!;
        var projectionHttp = Assert.Single(projection.GetCustomAttributes<HttpMethodAttribute>());
        Assert.Equal(["GET"], projectionHttp.HttpMethods);
        Assert.Equal("projection", projectionHttp.Template);
        Assert.Equal(
            $"Permission:{ProductAbbreviationPermissions.Read}",
            projection.GetCustomAttribute<HasPermissionAttribute>()!.Policy);

        var action = type.GetMethod(nameof(ProductAbbreviationWorkItemsController.DispatchAction))!;
        var actionHttp = Assert.Single(action.GetCustomAttributes<HttpMethodAttribute>());
        Assert.Equal(["POST"], actionHttp.HttpMethods);
        Assert.Equal("{itemId:guid}/actions/{actionCode}", actionHttp.Template);
    }

    [Fact]
    public void Wire_identity_actions_and_projection_field_count_are_frozen()
    {
        Assert.Equal("mdm-product-abbreviations", ProductAbbreviationWorkItemContract.ProviderCode);
        Assert.Equal("1.0", ProductAbbreviationWorkItemContract.ContractVersion);
        Assert.Equal(
            ["approve", "cancel", "reject"],
            ProductAbbreviationWorkItemContract.ActionCodes.Order().ToArray());
        Assert.Equal(31, typeof(ProductAbbreviationWorkItemProjection).GetProperties().Length);
        Assert.Equal(
            ["Payload", "ProviderCode", "UnmappedFields"],
            typeof(ProductAbbreviationWorkItemActionRequest).GetProperties()
                .Where(x => x.Name != nameof(ProductAbbreviationWorkItemActionRequest.HasUnmappedFields))
                .Select(x => x.Name).Order().ToArray());
    }

    [Fact]
    public void Dedicated_envelope_serializes_reason_code_and_exact_five_members()
    {
        var json = JsonSerializer.Serialize(
            ProductAbbreviationWorkItemEnvelope<object>.Fail(503, "ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(5, root.EnumerateObject().Count());
        Assert.Equal("ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED", root.GetProperty("reason_code").GetString());
        Assert.False(root.GetProperty("isSuccessful").GetBoolean());
    }

    [Fact]
    public void Full_shared_action_union_accepts_null_unused_members_but_rejects_caller_identity_override()
    {
        const string wire = """
            {
              "providerCode":"mdm-product-abbreviations",
              "payload":{
                "expectedVersion":0,
                "reason":null,
                "reasonCode":null,
                "note":null,
                "plannedDate":null,
                "assigneeUserId":null,
                "waitingOnUserId":null,
                "comment":null,
                "evidenceRef":null,
                "targetPrincipalId":null,
                "idempotencyKey":null
              }
            }
            """;
        var request = JsonSerializer.Deserialize<ProductAbbreviationWorkItemActionRequest>(
            wire,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.False(request.HasUnmappedFields);
        Assert.True((request with { Payload = request.Payload! with { IdempotencyKey = "caller-key" } }).HasUnmappedFields);
        Assert.True((request with { Payload = request.Payload! with { PlannedDate = DateTimeOffset.UtcNow } }).HasUnmappedFields);

        var unknown = JsonSerializer.Deserialize<ProductAbbreviationWorkItemActionRequest>(
            """{"providerCode":"mdm-product-abbreviations","unexpected":true,"payload":{"expectedVersion":0}}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(unknown.HasUnmappedFields);
    }
}
