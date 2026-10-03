namespace Diten.SupplyChainService.Persistence.Features.Returns;
public interface IReturnCommitProbe
{
    Task AtAsync(string boundary, CancellationToken cancellationToken);
    Guid NewId();
}
