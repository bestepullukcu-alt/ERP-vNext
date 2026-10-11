namespace Diten.SupplyChainService.Persistence.Features.Returns;
public sealed class NoOpReturnCommitProbe : IReturnCommitProbe
{
    public Task AtAsync(string boundary, CancellationToken cancellationToken) => Task.CompletedTask;
    public Guid NewId() => Guid.NewGuid();
}
