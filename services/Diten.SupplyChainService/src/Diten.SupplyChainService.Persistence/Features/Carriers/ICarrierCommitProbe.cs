namespace Diten.SupplyChainService.Persistence.Features.Carriers;
public interface ICarrierCommitProbe
{
    Task AtAsync(string phase, CancellationToken ct);
}
