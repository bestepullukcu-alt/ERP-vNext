using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IIndustrySkillPassportReadinessMetadataRepository
{
    Task<IReadOnlyList<IndustrySkillPassportReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<IndustrySkillPassportReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct);
}
