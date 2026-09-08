using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITalentDataFoundationReadinessMetadataRepository
{
    Task<IReadOnlyList<TalentDataFoundationReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TalentDataFoundationReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TalentDataFoundationReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TalentDataFoundationReadinessMetadata metadata, CancellationToken ct);
}
