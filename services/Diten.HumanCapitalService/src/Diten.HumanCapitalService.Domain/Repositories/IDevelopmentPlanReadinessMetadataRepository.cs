using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IDevelopmentPlanReadinessMetadataRepository
{
    Task<IReadOnlyList<DevelopmentPlanReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<DevelopmentPlanReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(DevelopmentPlanReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(DevelopmentPlanReadinessMetadata metadata, CancellationToken ct);
}
