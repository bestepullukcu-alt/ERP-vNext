using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepCandidateDisputeReadinessMetadataRepository
{
    Task<IReadOnlyList<TepCandidateDisputeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepCandidateDisputeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct);
}
