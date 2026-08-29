using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IGskuRepository
{
    Task<Gsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Gsku>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task<GskuPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default);
    Task<GskuPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        bool referenceableOnly,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_LEGAL_ENTITY_SCOPE_READ_CONTRACT_NOT_IMPLEMENTED");
    Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default) =>
        GetReferenceablePageAsync(pageNumber, pageSize, canonicalCodeSearch, cancellationToken);
    Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(
        string canonicalCodeSearch,
        CancellationToken cancellationToken = default);
    Task<Gsku?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default);
    Task<GskuCreateResult> CreateDraftAsync(Gsku gsku, CancellationToken cancellationToken = default);
    Task<GskuUpdateResult> UpdateDraftAsync(Gsku gsku, int expectedVersion, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> MarkIdentityPendingAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_IDENTITY_LIFECYCLE_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> ApproveIdentityAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_IDENTITY_LIFECYCLE_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> RestoreDraftAfterRejectionAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_IDENTITY_LIFECYCLE_NOT_IMPLEMENTED");
    Task<GskuChildCreationAdmissionResult> AcquireChildCreationAdmissionAsync(
        Guid id, GskuChildIdentityKind childKind, string creationCommandId,
        string requestFingerprint, DateTimeOffset acquiredAtUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");
    Task<GskuChildCreationAdmissionResult> CompleteChildCreationAdmissionAsync(
        Guid id, GskuChildIdentityKind childKind, string creationCommandId,
        string requestFingerprint, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityRetirementWriteResult<Gsku>> CloseChildAdmissionFenceAsync(
        Guid id, int expectedVersion, Guid operationId, string operationFingerprint,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_RETIREMENT_NOT_IMPLEMENTED");
    Task<string?> FindRetirementBlockerAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_RETIREMENT_NOT_IMPLEMENTED");
    Task<bool> HasNonRetiredSiblingAsync(
        Guid productDefinitionRevisionId, Guid excludingGskuId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_RETIREMENT_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityRetirementWriteResult<Gsku>> RetireIdentityAsync(
        Guid id, int expectedVersion, Guid operationId, string operationFingerprint,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_RETIREMENT_NOT_IMPLEMENTED");
}

public sealed record GskuCreateResult(bool Succeeded, Gsku? Gsku, string? ErrorCode = null);
public sealed record GskuUpdateResult(bool Succeeded, Gsku? Gsku, string? ErrorCode = null);
public sealed record GskuPage(IReadOnlyList<Gsku> Items, long TotalCount);
