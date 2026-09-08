using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IPerformanceReviewReadinessMetadataRepository
{
    Task<IReadOnlyList<PerformanceReviewReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<PerformanceReviewReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct);
}
