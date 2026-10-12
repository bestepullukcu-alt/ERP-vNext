namespace Diten.SupplyChainService.Persistence.Features.Shipments;
public sealed class NoOpShipmentCommitProbe : IShipmentCommitProbe
{
    public Task BeforeCommitAsync(CancellationToken ct) => Task.CompletedTask;
}
