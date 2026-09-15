using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IPerformanceReviewReadinessMetadataRepository
{
    Task<IReadOnlyList<PerformanceReviewReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<PerformanceReviewReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct);
}
