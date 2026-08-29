using System.Text.Json;
using Diten.Application.WorkCenter;

namespace Diten.EnterpriseStrategy.EndToEnd.Tests.WorkCenter;

public sealed class EnterpriseStrategyWorkItemEndpointTests
{
    [Fact]
    public async Task Remote_read_preparation_is_default_off_non_migrating_and_cancellation_safe()
    {
        var service = new EnterpriseStrategyWorkItemProjectionService(false);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAsync("self", source.Token));

        var result = await service.GetAsync("self");
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(EnterpriseStrategyWorkItemContract.TenantSafetyNotReady, result.ReasonCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Contract_carries_exactly_fifty_projection_fields_and_no_action_route_constant()
    {
        Assert.Equal(50, typeof(EnterpriseStrategyWorkItemProjection).GetProperties().Length);
        Assert.Equal("api/v1/work-items/projection", EnterpriseStrategyWorkItemContract.ProjectionRoute);
        Assert.DoesNotContain("action", typeof(EnterpriseStrategyWorkItemContract)
            .GetFields().Select(field => field.Name), StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(EnterpriseStrategyWorkItemEnvelope.TenantSafetyUnavailable());
        Assert.DoesNotContain(EnterpriseStrategyWorkItemContract.ProviderCode, json, StringComparison.Ordinal);
    }
}
