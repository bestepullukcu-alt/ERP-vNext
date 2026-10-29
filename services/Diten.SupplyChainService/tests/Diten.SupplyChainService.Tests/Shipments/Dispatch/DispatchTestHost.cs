using System.Reflection;
using System.Runtime.ExceptionServices;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Shipments;
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

/// <summary>The real repository with a hook run once just before the next call of one method — a race made deterministic
/// (review F-7: a Cancelled between the dispatch's read and its conditional begin; re-review N-1 / R-3).</summary>
public class HookedShipmentRepository : DispatchProxy
{
    private IShipmentRepository _inner = null!;
    private Func<string, Func<Task>?> _takeHook = null!;
    public static IShipmentRepository Wrap(IShipmentRepository inner, Func<string, Func<Task>?> takeHook)
    {
        var proxy = Create<IShipmentRepository, HookedShipmentRepository>();
        var hooked = (HookedShipmentRepository)(object)proxy; hooked._inner = inner; hooked._takeHook = takeHook;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (_takeHook(method!.Name) is { } hook) hook().GetAwaiter().GetResult();
        try { return method.Invoke(_inner, args); }
        catch (TargetInvocationException e) when (e.InnerException is not null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
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
