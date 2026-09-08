using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IIndustrySuccessionPoolReadinessMetadataRepository
{
    Task<IReadOnlyList<IndustrySuccessionPoolReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<IndustrySuccessionPoolReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(IndustrySuccessionPoolReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(IndustrySuccessionPoolReadinessMetadata metadata, CancellationToken ct);
}
