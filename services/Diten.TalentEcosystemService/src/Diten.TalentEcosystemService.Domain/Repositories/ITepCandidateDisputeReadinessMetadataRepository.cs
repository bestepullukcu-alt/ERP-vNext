using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepCandidateDisputeReadinessMetadataRepository
{
    Task<IReadOnlyList<TepCandidateDisputeReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepCandidateDisputeReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct);
}
