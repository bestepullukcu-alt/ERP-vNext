using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepCandidateProfileMetadataRepository
{
    Task<IReadOnlyList<TepCandidateProfileMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepCandidateProfileMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct);
}
