using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ISuccessionReadinessMetadataRepository
{
    Task<IReadOnlyList<SuccessionReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<SuccessionReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SuccessionReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SuccessionReadinessMetadata metadata, CancellationToken ct);
}
