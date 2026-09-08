using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IMetricSemanticRegistryReadinessMetadataRepository
{
    Task<IReadOnlyList<MetricSemanticRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<MetricSemanticRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(MetricSemanticRegistryReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(MetricSemanticRegistryReadinessMetadata metadata, CancellationToken ct);
}
