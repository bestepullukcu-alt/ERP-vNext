using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IIndustryTalentPoolReadinessMetadataRepository
{
    Task<IReadOnlyList<IndustryTalentPoolReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<IndustryTalentPoolReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(IndustryTalentPoolReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(IndustryTalentPoolReadinessMetadata metadata, CancellationToken ct);
}
