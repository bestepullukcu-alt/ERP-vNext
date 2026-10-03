namespace Diten.SupplyChainService.Persistence.Features.Claims;
public interface IClaimCommitProbe
{
    Guid NewId();
    Task AtAsync(string stage, CancellationToken cancellationToken);
}
