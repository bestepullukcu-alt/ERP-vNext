using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IMetricDefinitionsOwnershipReadinessMetadataRepository
{
    Task<IReadOnlyList<MetricDefinitionsOwnershipReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<MetricDefinitionsOwnershipReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(MetricDefinitionsOwnershipReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(MetricDefinitionsOwnershipReadinessMetadata metadata, CancellationToken ct);
}
