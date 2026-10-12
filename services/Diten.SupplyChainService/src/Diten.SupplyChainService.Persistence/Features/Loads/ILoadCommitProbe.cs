namespace Diten.SupplyChainService.Persistence.Features.Loads;
public interface ILoadCommitProbe { Task AtAsync(string boundary,CancellationToken ct); Guid NewId(); }
