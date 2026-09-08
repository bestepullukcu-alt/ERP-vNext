using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ISuccessionReadinessMetadataRepository
{
    Task<IReadOnlyList<SuccessionReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<SuccessionReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SuccessionReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SuccessionReadinessMetadata metadata, CancellationToken ct);
}
