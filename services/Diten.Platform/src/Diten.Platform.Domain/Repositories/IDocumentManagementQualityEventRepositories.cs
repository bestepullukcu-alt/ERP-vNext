using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU22 — quality event / deviation / CAPA / source link contracts. Tenant-scoped via the
// TenantRepository ExecutionFilter. No delete method anywhere: a quality record's history is the evidence, so
// cancellation and closure are status changes.

public interface IDocumentQualityEventRepository
{
    Task<DocumentQualityEvent> CreateAsync(DocumentQualityEvent qualityEvent, CancellationToken ct = default);
    Task<DocumentQualityEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentQualityEvent>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentQualityEvent>> GetOpenAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocumentQualityEvent>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentQualityEvent qualityEvent, CancellationToken ct = default);
}

public interface IDocumentDeviationRepository
{
    Task<DocumentDeviation> CreateAsync(DocumentDeviation deviation, CancellationToken ct = default);
    Task<DocumentDeviation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDeviation>> GetByQualityEventAsync(Guid qualityEventId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDeviation>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentDeviation deviation, CancellationToken ct = default);
}

public interface IDocumentCAPAActionRepository
{
    Task<DocumentCAPAAction> CreateAsync(DocumentCAPAAction action, CancellationToken ct = default);
    Task<DocumentCAPAAction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCAPAAction>> GetByQualityEventAsync(Guid qualityEventId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCAPAAction>> GetByDeviationAsync(Guid deviationId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCAPAAction>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentCAPAAction action, CancellationToken ct = default);
}

public interface IDocumentQualityEventSourceLinkRepository
{
    Task<DocumentQualityEventSourceLink> CreateAsync(DocumentQualityEventSourceLink link, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentQualityEventSourceLink>> GetByQualityEventAsync(Guid qualityEventId, CancellationToken ct = default);

    /// <summary>The bridge idempotency lookup: has this source already raised an event of this type?</summary>
    Task<IReadOnlyList<DocumentQualityEventSourceLink>> GetBySourceAsync(
        QualityEventSourceType sourceType, Guid sourceId, CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentQualityEventSourceLink link, CancellationToken ct = default);
}
