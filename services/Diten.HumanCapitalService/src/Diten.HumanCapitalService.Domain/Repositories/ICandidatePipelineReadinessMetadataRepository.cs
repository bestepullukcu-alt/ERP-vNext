using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ICandidatePipelineReadinessMetadataRepository
{
    Task<IReadOnlyList<CandidatePipelineReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<CandidatePipelineReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(CandidatePipelineReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(CandidatePipelineReadinessMetadata metadata, CancellationToken ct);
}
