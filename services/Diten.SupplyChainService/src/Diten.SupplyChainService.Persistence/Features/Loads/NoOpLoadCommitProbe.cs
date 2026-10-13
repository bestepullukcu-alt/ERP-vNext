namespace Diten.SupplyChainService.Persistence.Features.Loads;
public sealed class NoOpLoadCommitProbe:ILoadCommitProbe { public Task AtAsync(string boundary,CancellationToken ct)=>Task.CompletedTask; public Guid NewId()=>Guid.NewGuid(); }
