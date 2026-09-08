using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IEtlEltPipelinesReadinessMetadataRepository
{
    Task<IReadOnlyList<EtlEltPipelinesReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<EtlEltPipelinesReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EtlEltPipelinesReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(EtlEltPipelinesReadinessMetadata metadata, CancellationToken ct);
}
