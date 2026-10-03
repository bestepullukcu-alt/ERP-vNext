namespace Diten.SupplyChainService.Domain.Features.Loads;
public interface ILoadRepository
{
 Task<IReadOnlyList<LoadReadResult>> QueryAsync(LoadScope scope, LoadStatus? status, Guid? carrier, CancellationToken ct);
 Task<LoadMutationResult> MutateAsync(LoadScope scope, Guid? id, string key, string fingerprint, Guid root,
 LoadPlan? create, LoadStatus? target, string? occurredAt, string? note,
 Func<LoadPlan,Guid?,CancellationToken,Task<string>> observe, CancellationToken ct);
}
