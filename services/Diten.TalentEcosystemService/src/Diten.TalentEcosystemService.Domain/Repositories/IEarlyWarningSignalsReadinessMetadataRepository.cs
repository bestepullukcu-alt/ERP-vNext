using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IEarlyWarningSignalsReadinessMetadataRepository
{
    Task<IReadOnlyList<EarlyWarningSignalsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<EarlyWarningSignalsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EarlyWarningSignalsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(EarlyWarningSignalsReadinessMetadata metadata, CancellationToken ct);
}
