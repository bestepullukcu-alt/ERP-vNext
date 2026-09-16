using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ISelfServiceReadinessMetadataRepository
{
    Task<IReadOnlyList<SelfServiceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<SelfServiceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct);
}
