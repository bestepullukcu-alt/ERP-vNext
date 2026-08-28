using System.Reflection;
using System.Text.Json;
using Diten.Application.WorkCenter;
using Diten.WebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Application.Tests.WorkCenter;

public sealed class EnterpriseStrategyWorkItemProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preparation_mode_is_exact_503_and_has_no_data_or_actions(bool enabled)
    {
        var service = new EnterpriseStrategyWorkItemProjectionService(enabled);

        var result = await service.GetAsync("self");

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(EnterpriseStrategyWorkItemContract.TenantSafetyNotReady, result.ReasonCode);
        Assert.Null(result.Data);
        Assert.Single(result.Errors, EnterpriseStrategyWorkItemContract.TenantSafetyNotReady);
        Assert.Single(typeof(EnterpriseStrategyWorkItemProjectionService).GetConstructors());
        Assert.Equal([typeof(bool)], typeof(EnterpriseStrategyWorkItemProjectionService)
            .GetConstructors().Single().GetParameters().Select(x => x.ParameterType).ToArray());
    }

    [Fact]
    public async Task Controller_exposes_only_the_default_get_and_returns_the_same_fail_closed_envelope()
    {
        var controller = new WorkItemsController(new EnterpriseStrategyWorkItemProjectionService(false));
        var action = typeof(WorkItemsController).GetMethod(nameof(WorkItemsController.Get))!;

        var response = await controller.Get("team");
        var objectResult = Assert.IsType<ObjectResult>(response.Result);

        Assert.Equal(503, objectResult.StatusCode);
        Assert.Equal("api/v1/work-items/projection",
            typeof(WorkItemsController).GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
        Assert.DoesNotContain(typeof(WorkItemsController).GetMethods(), method =>
            method.GetCustomAttributes().Any(attribute => attribute is HttpPostAttribute));
    }

    [Fact]
    public void Envelope_serialization_uses_the_exact_DCP_004_failure_members()
    {
        var json = JsonSerializer.Serialize(EnterpriseStrategyWorkItemEnvelope.TenantSafetyUnavailable());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
        Assert.Equal(503, root.GetProperty("statusCode").GetInt32());
        Assert.False(root.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal(EnterpriseStrategyWorkItemContract.TenantSafetyNotReady,
            root.GetProperty("reason_code").GetString());
        Assert.Equal(5, root.EnumerateObject().Count());
    }
}
