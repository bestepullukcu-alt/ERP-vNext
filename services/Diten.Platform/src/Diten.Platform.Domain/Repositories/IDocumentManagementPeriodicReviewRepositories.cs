using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU12 — periodic review / extension / escalation repository contracts. Tenant-scoped; never hard-deleted.

public interface IDocumentPeriodicReviewRepository
{
    Task<DocumentPeriodicReview> CreateAsync(DocumentPeriodicReview review, CancellationToken ct = default);
    Task<DocumentPeriodicReview?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentPeriodicReview>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>The most recent review that is not Completed/Cancelled, if any.</summary>
    Task<DocumentPeriodicReview?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentPeriodicReview review, CancellationToken ct = default);
}

public interface IDocumentPeriodicReviewExtensionRepository
{
    Task<DocumentPeriodicReviewExtension> CreateAsync(DocumentPeriodicReviewExtension extension, CancellationToken ct = default);
    Task<DocumentPeriodicReviewExtension?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentPeriodicReviewExtension>> GetByReviewAsync(Guid periodicReviewId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentPeriodicReviewExtension extension, CancellationToken ct = default);
}

public interface IDocumentPeriodicReviewEscalationRepository
{
    Task<DocumentPeriodicReviewEscalation> CreateAsync(DocumentPeriodicReviewEscalation escalation, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByReviewAsync(Guid periodicReviewId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
}
