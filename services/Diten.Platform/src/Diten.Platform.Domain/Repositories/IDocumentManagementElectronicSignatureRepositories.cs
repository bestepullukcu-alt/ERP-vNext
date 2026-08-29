using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU23 — electronic signature policy / request / record / fingerprint contracts. Tenant-scoped via the
// TenantRepository ExecutionFilter.
//
// NOTE THE ABSENCE OF DELETE, AND OF ANY UPDATE ON A FINGERPRINT: a signature and the object state it was applied
// to are the evidence. Invalidation is an UpdateAsync that sets status and reason; a fingerprint row is written
// once and never revised.

public interface IDocumentSignaturePolicyRepository
{
    Task<DocumentSignaturePolicy> CreateAsync(DocumentSignaturePolicy policy, CancellationToken ct = default);
    Task<DocumentSignaturePolicy?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tenant-scoped uniqueness lookup for <c>PolicyKey</c>.</summary>
    Task<DocumentSignaturePolicy?> GetByKeyAsync(string policyKey, CancellationToken ct = default);

    /// <summary>Active policies for a subject type — the candidate set the most-restrictive-wins rule picks from.</summary>
    Task<IReadOnlyList<DocumentSignaturePolicy>> GetActiveBySubjectTypeAsync(
        SignableSubjectType subjectType, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentSignaturePolicy>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentSignaturePolicy policy, CancellationToken ct = default);
}

public interface IDocumentSignatureRequestRepository
{
    Task<DocumentSignatureRequest> CreateAsync(DocumentSignatureRequest request, CancellationToken ct = default);
    Task<DocumentSignatureRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentSignatureRequest>> GetBySubjectAsync(
        SignableSubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentSignatureRequest>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentSignatureRequest request, CancellationToken ct = default);
}

public interface IDocumentSignatureRecordRepository
{
    Task<DocumentSignatureRecord> CreateAsync(DocumentSignatureRecord record, CancellationToken ct = default);
    Task<DocumentSignatureRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The signature history for one subject — every attestation, valid or not.</summary>
    Task<IReadOnlyList<DocumentSignatureRecord>> GetBySubjectAsync(
        SignableSubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentSignatureRecord>> GetByRequestAsync(Guid signatureRequestId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentSignatureRecord>> GetAllForTenantAsync(CancellationToken ct = default);

    /// <summary>Status/verification updates only. There is no delete: a signature's history is the evidence.</summary>
    Task<bool> UpdateAsync(DocumentSignatureRecord record, CancellationToken ct = default);
}

public interface IDocumentSignedObjectFingerprintRepository
{
    Task<DocumentSignedObjectFingerprint> CreateAsync(DocumentSignedObjectFingerprint fingerprint, CancellationToken ct = default);
    Task<DocumentSignedObjectFingerprint?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Newest first — the fingerprint history for a subject.</summary>
    Task<IReadOnlyList<DocumentSignedObjectFingerprint>> GetBySubjectAsync(
        SignableSubjectType subjectType, Guid subjectId, CancellationToken ct = default);
}
