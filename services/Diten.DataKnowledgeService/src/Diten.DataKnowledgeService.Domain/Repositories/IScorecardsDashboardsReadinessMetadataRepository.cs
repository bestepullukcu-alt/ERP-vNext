using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IScorecardsDashboardsReadinessMetadataRepository
{
    Task<IReadOnlyList<ScorecardsDashboardsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<ScorecardsDashboardsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(ScorecardsDashboardsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(ScorecardsDashboardsReadinessMetadata metadata, CancellationToken ct);
}
