using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IAssociationOperationsReadinessMetadataRepository
{
    Task<IReadOnlyList<AssociationOperationsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<AssociationOperationsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(AssociationOperationsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(AssociationOperationsReadinessMetadata metadata, CancellationToken ct);
}
