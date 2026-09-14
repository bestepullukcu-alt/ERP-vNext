using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IIndustrySkillPassportReadinessMetadataRepository
{
    Task<IReadOnlyList<IndustrySkillPassportReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<IndustrySkillPassportReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct);
}
