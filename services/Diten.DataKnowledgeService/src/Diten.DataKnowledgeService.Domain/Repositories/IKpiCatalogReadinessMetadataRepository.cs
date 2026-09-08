using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IKpiCatalogReadinessMetadataRepository
{
    Task<IReadOnlyList<KpiCatalogReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<KpiCatalogReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(KpiCatalogReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(KpiCatalogReadinessMetadata metadata, CancellationToken ct);
}
