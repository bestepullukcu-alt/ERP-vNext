using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IHrDocumentationReadinessMetadataRepository
{
    Task<IReadOnlyList<HrDocumentationReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<HrDocumentationReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HrDocumentationReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HrDocumentationReadinessMetadata metadata, CancellationToken ct);
}
