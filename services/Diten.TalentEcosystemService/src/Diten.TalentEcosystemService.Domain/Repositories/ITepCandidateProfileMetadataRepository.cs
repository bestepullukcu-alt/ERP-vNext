using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepCandidateProfileMetadataRepository
{
    Task<IReadOnlyList<TepCandidateProfileMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepCandidateProfileMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct);
}
