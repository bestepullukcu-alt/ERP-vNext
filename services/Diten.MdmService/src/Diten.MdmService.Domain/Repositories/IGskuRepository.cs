using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
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
    Task<GskuPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        bool referenceableOnly,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) => lifecycleStatus is null
            ? GetEnforcedLegalEntityScopePageAsync(pageNumber, pageSize, canonicalCodeSearch, referenceableOnly,
                effectiveCandidateLegalEntityIds, serverNowUtc, cancellationToken)
            : throw new NotSupportedException("GSKU_LIFECYCLE_FILTER_NOT_IMPLEMENTED");
    // FIX3 B17: the register sorted by a whitelisted stored field, id as the stable tiebreak.
    Task<GskuPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        bool referenceableOnly,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        GskuListSort sort,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) => sort == GskuListSort.Default
            ? GetEnforcedLegalEntityScopePageAsync(pageNumber, pageSize, canonicalCodeSearch, referenceableOnly,
                lifecycleStatus, effectiveCandidateLegalEntityIds, serverNowUtc, cancellationToken)
            : throw new NotSupportedException("GSKU_LIST_SORT_NOT_IMPLEMENTED");
    Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default) =>
        GetReferenceablePageAsync(pageNumber, pageSize, canonicalCodeSearch, cancellationToken);
    Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        CancellationToken cancellationToken = default) => lifecycleStatus is null
            ? GetPageAsync(pageNumber, pageSize, canonicalCodeSearch, cancellationToken)
            : throw new NotSupportedException("GSKU_LIFECYCLE_FILTER_NOT_IMPLEMENTED");
    Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        GskuListSort sort,
        CancellationToken cancellationToken = default) => sort == GskuListSort.Default
            ? GetPageAsync(pageNumber, pageSize, canonicalCodeSearch, lifecycleStatus, cancellationToken)
            : throw new NotSupportedException("GSKU_LIST_SORT_NOT_IMPLEMENTED");
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
    // LSKU FIX1 B1 (b): a creation that ends without its child row gives the admission back in the same flow. The
    // admission goes only while no child row carries this command, so a write whose outcome is unknown keeps it.
    Task<GskuChildCreationAdmissionResult> ReleaseChildCreationAdmissionAsync(
        Guid id, GskuChildIdentityKind childKind, string creationCommandId,
        string requestFingerprint, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");
    // LSKU FIX1 B1 (c): the operator repair's read — admissions older than the cutoff whose child row never came.
    Task<IReadOnlyList<GskuOrphanChildAdmission>> FindOrphanChildCreationAdmissionsAsync(
        GskuChildIdentityKind childKind, DateTimeOffset acquiredBeforeUtc, int limit,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");
    // LSKU FIX1 B1 (c): the repair's write — the admission goes and the intent that says so is kept, in one write.
    Task<GskuChildCreationAdmissionResult> RepairOrphanChildCreationAdmissionAsync(
        GskuOrphanChildAdmission orphan, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");
    // CT 2026-10-06: the stuck-admission gauge — admissions of any kind held past the cutoff, and the tenants holding any.
    Task<long> CountStaleChildCreationAdmissionsAsync(DateTimeOffset acquiredBeforeUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");
    Task<IReadOnlyList<Guid>> DiscoverTenantsWithChildAdmissionsAsync(Guid? afterTenantId, int limit,
        CancellationToken cancellationToken = default) =>
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
    Task<GskuCorrectionWriteResult> AcquireCorrectionAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CORRECTION_NOT_IMPLEMENTED");
    Task<GskuCorrectionWriteResult> ApplyCorrectionDecisionAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        decimal? approvedPackQuantity, string? approvedPackUomCode,
        ReferenceCatalogSelection? approvedPackApplicabilitySelection,
        ReferenceCatalogSelection? approvedPackUomSelection,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CORRECTION_NOT_IMPLEMENTED");
    Task<GskuRetirementRequestWriteResult> AcquireRetirementRequestAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_RETIREMENT_REQUEST_NOT_IMPLEMENTED");
    Task<GskuRetirementRequestWriteResult> RejectRetirementRequestAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_RETIREMENT_REQUEST_NOT_IMPLEMENTED");
    Task<GskuRetirementRequestWriteResult> RecordRetirementRequestConflictAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_RETIREMENT_REQUEST_NOT_IMPLEMENTED");
    // B3: gives the GSKU back from a correction / retirement request that ends without its decision applied; the
    // release and the intent that says why are one write.
    Task<GskuCorrectionWriteResult> ReleaseLifecycleOperationAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_LIFECYCLE_RELEASE_NOT_IMPLEMENTED");
}

public sealed record GskuCreateResult(bool Succeeded, Gsku? Gsku, string? ErrorCode = null);
public sealed record GskuUpdateResult(bool Succeeded, Gsku? Gsku, string? ErrorCode = null);
public sealed record GskuPage(IReadOnlyList<Gsku> Items, long TotalCount);
