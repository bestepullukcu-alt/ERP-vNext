using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Infrastructure.Features.Shipments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.SupplyChainService.Tests.Shipments.Dispatch;

/// <summary>A test clock the dispatch lease reads (§23.4: lease = request timeout + 30 s); only GetUtcNow moves.</summary>
public sealed class ManualClock : TimeProvider
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

public static class DispatchTestHost
{
    public const string InventoryBaseUrl = "http://inventory.test/";

    /// <summary>The real InventoryMovementClient over the fake INVENTORY handler, and optionally the test clock.</summary>
    public static IServiceCollection UseFakeInventory(this IServiceCollection services, FakeInventory fake, TimeProvider? clock = null)
    {
        services.AddHttpClient<IInventoryMovementClient, InventoryMovementClient>().ConfigurePrimaryHttpMessageHandler(() => fake);
        if (clock is not null) services.Replace(ServiceDescriptor.Singleton(clock));
        return services;
    }
}
