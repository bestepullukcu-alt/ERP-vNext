using Diten.InventoryService.Application.Features.CycleCounting.Ports;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>The production <see cref="ICycleCountReleaseProbe"/>: nothing happens between the two snapshot reads.</summary>
public sealed class NoopCycleCountReleaseProbe : ICycleCountReleaseProbe
{
    public Task AfterBalanceReadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
