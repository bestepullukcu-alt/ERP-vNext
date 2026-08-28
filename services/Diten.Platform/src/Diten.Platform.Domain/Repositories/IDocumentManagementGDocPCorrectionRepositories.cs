using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU21 — GDocP correction trail contracts. Tenant-scoped via the TenantRepository ExecutionFilter.
// There is deliberately NO delete method: the correction trail must be non-erasable, which is the whole point of
// it. Note that the correction record repository has no general Update either — only the narrow review path
// mutates a record, and it does so through UpdateReviewAsync.

public interface IDocumentGDocPCorrectionRecordRepository
{
    Task<DocumentGDocPCorrectionRecord> CreateAsync(DocumentGDocPCorrectionRecord record, CancellationToken ct = default);
    Task<DocumentGDocPCorrectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetBySubjectAsync(GDocPSubjectType subjectType, Guid subjectId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetPendingReviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetAllForTenantAsync(CancellationToken ct = default);

    /// <summary>
    /// The ONLY mutation path on a correction record: applying a review verdict. The recorded field path, values,
    /// reason and correction timestamp are never rewritten.
    /// </summary>
    Task<bool> UpdateReviewAsync(DocumentGDocPCorrectionRecord record, CancellationToken ct = default);
}

public interface IDocumentGDocPCorrectionPolicyRepository
{
    Task<DocumentGDocPCorrectionPolicy> CreateAsync(DocumentGDocPCorrectionPolicy policy, CancellationToken ct = default);
    Task<DocumentGDocPCorrectionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentGDocPCorrectionPolicy?> GetByKeyAsync(string policyKey, CancellationToken ct = default);

    /// <summary>Active policies for a subject type — the candidate set for the most-restrictive resolution.</summary>
    Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetActiveBySubjectTypeAsync(GDocPSubjectType subjectType, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentGDocPCorrectionPolicy policy, CancellationToken ct = default);
}

public interface IDocumentGDocPCorrectionReviewRepository
{
    Task<DocumentGDocPCorrectionReview> CreateAsync(DocumentGDocPCorrectionReview review, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentGDocPCorrectionReview>> GetByCorrectionAsync(Guid correctionRecordId, CancellationToken ct = default);
}
