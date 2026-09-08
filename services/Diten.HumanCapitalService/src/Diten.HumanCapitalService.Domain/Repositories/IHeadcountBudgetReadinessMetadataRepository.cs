using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IHeadcountBudgetReadinessMetadataRepository
{
    Task<IReadOnlyList<HeadcountBudgetReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<HeadcountBudgetReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HeadcountBudgetReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HeadcountBudgetReadinessMetadata metadata, CancellationToken ct);
}
