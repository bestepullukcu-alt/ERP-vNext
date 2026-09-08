using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ISelfServiceReadinessMetadataRepository
{
    Task<IReadOnlyList<SelfServiceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<SelfServiceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct);
}
