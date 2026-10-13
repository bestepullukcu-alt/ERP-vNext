namespace Diten.SupplyChainService.Persistence.Features.Carriers;
public sealed class NoOpCarrierCommitProbe : ICarrierCommitProbe
{
    public Task AtAsync(string phase, CancellationToken ct) => Task.CompletedTask;
}
