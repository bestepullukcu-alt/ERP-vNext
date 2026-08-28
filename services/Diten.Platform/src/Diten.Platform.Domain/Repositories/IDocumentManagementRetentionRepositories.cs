using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU15 — retention policy / subject / legal hold / disposition repository contracts. Every method is
// tenant-scoped via the TenantRepository ExecutionFilter. NOTE: there is deliberately NO delete method anywhere in
// this file — FU15 is a retention foundation, not a destruction engine.

public interface IDocumentRetentionPolicyRepository
{
    Task<DocumentRetentionPolicy> CreateAsync(DocumentRetentionPolicy policy, CancellationToken ct = default);
    Task<DocumentRetentionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentRetentionPolicy?> GetByKeyAsync(string policyKey, CancellationToken ct = default);

    /// <summary>Active policies governing a subject type — the candidate set for the longest-applicable comparison.</summary>
    Task<IReadOnlyList<DocumentRetentionPolicy>> GetActiveBySubjectTypeAsync(RetentionSubjectType subjectType, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRetentionPolicy>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentRetentionPolicy policy, CancellationToken ct = default);
}

public interface IDocumentRetentionSubjectRepository
{
    Task<DocumentRetentionSubject> CreateAsync(DocumentRetentionSubject subject, CancellationToken ct = default);
    Task<DocumentRetentionSubject?> GetBySubjectAsync(RetentionSubjectType subjectType, Guid subjectId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRetentionSubject>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>Subjects whose retention has elapsed with no active hold — disposition REQUEST candidates only.</summary>
    Task<IReadOnlyList<DocumentRetentionSubject>> GetEligibleAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRetentionSubject>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentRetentionSubject subject, CancellationToken ct = default);
}

public interface IDocumentLegalHoldRepository
{
    Task<DocumentLegalHold> CreateAsync(DocumentLegalHold hold, CancellationToken ct = default);
    Task<DocumentLegalHold?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentLegalHold>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocumentLegalHold>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentLegalHold hold, CancellationToken ct = default);
}

public interface IDocumentLegalHoldSubjectRepository
{
    Task<DocumentLegalHoldSubject> CreateAsync(DocumentLegalHoldSubject subject, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentLegalHoldSubject>> GetByHoldAsync(Guid legalHoldId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentLegalHoldSubject>> GetBySubjectAsync(RetentionSubjectType subjectType, Guid subjectId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentLegalHoldSubject subject, CancellationToken ct = default);
}

public interface IDocumentDispositionRequestRepository
{
    Task<DocumentDispositionRequest> CreateAsync(DocumentDispositionRequest request, CancellationToken ct = default);
    Task<DocumentDispositionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDispositionRequest>> GetBySubjectAsync(RetentionSubjectType subjectType, Guid subjectId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDispositionRequest>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentDispositionRequest request, CancellationToken ct = default);
}
