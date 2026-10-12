namespace Diten.SupplyChainService.Persistence.Features.Shipments;
public interface IShipmentCommitProbe
{
    Task BeforeCommitAsync(CancellationToken ct);
}
