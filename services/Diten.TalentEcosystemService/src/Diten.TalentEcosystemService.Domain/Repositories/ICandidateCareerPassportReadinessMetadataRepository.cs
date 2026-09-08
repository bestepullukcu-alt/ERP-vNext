using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ICandidateCareerPassportReadinessMetadataRepository
{
    Task<IReadOnlyList<CandidateCareerPassportReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<CandidateCareerPassportReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(CandidateCareerPassportReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(CandidateCareerPassportReadinessMetadata metadata, CancellationToken ct);
}
