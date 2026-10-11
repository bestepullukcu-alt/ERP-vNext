namespace Diten.SupplyChainService.Persistence.Features.Claims;
public sealed class NoOpClaimCommitProbe : IClaimCommitProbe
{
    public Guid NewId() => Guid.NewGuid();
    public Task AtAsync(string stage, CancellationToken cancellationToken) => Task.CompletedTask;
}
