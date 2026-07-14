using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0028-FU09 — tenant-scoped provisioning-evidence + deviation repositories. All methods are tenant-filtered via
// the TenantRepository ExecutionFilter; soft delete only (no hard delete).

public interface IProvisioningEvidenceRepository
{
    Task<DocumentCollectionProvisioningEvidence> CreateAsync(DocumentCollectionProvisioningEvidence evidence, CancellationToken ct = default);
    Task<DocumentCollectionProvisioningEvidence?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentCollectionProvisioningEvidence?> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCollectionProvisioningEvidence>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentCollectionProvisioningEvidence evidence, CancellationToken ct = default);
}

public interface IDocumentCollectionDeviationRepository
{
    Task<DocumentCollectionDeviation> CreateAsync(DocumentCollectionDeviation deviation, CancellationToken ct = default);
    Task<DocumentCollectionDeviation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCollectionDeviation>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default);

    /// <summary>Open deviations for a baseline, used for idempotent re-detection (match instead of duplicate).</summary>
    Task<IReadOnlyList<DocumentCollectionDeviation>> GetOpenByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentCollectionDeviation deviation, CancellationToken ct = default);
}
