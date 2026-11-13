using Diten.InventoryService.Application.Features.CycleCounting.Ports;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>The production submit probe: nothing between the transaction start and the ledger read.</summary>
public sealed class NoopCycleCountSubmitProbe : ICycleCountSubmitProbe
{
    public Task BeforeLedgerReadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
