using Diten.SafetyStockService.Application;

namespace Diten.SafetyStockService.Persistence;

public sealed class UnconfiguredManualQuantityStore : IManualQuantityStore
{
    public Task<ManualQuantityResult> ExecuteAsync(ManualQuantityCommand command,
        ActorSeriesScope scope, string baseUomId, CancellationToken ct)
        => Task.FromResult(new ManualQuantityResult(ManualQuantityStatus.Unavailable));

    public Task<ManualQuantityResult> ReadEffectiveAsync(EffectiveManualQuantityQuery query,
        ActorSeriesScope scope, string baseUomId, CancellationToken ct)
        => Task.FromResult(new ManualQuantityResult(ManualQuantityStatus.Unavailable));
}
