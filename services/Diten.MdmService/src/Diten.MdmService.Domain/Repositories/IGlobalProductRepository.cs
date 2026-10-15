using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductRepository
{
    Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    async Task<IReadOnlyList<GlobalProduct>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GlobalProduct>();
        foreach (var id in ids)
        {
            var item = await GetByIdAsync(id, cancellationToken);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }
    Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<bool> NameExistsOtherThanAsync(
        string normalizedName,
        Guid excludedProductId,
        CancellationToken cancellationToken = default) =>
        NameExistsAsync(normalizedName, cancellationToken);
    Task<GlobalProductPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        CancellationToken cancellationToken = default);
    Task<GlobalProductPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus: null, cancellationToken);
    Task<GlobalProductPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        bool referenceableOnly,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Enforced product Legal Entity scope paging is not implemented by this repository.");
    // WP-MDM-GP-LIST-01: the register sorted by a whitelisted stored field (default = the overloads above).
    Task<GlobalProductPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        GlobalProductListSort sort,
        CancellationToken cancellationToken = default) => sort == GlobalProductListSort.Default
            ? GetPageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus, cancellationToken)
            : throw new NotSupportedException("GLOBAL_PRODUCT_LIST_SORT_NOT_IMPLEMENTED");
    Task<GlobalProductPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        bool referenceableOnly,
        GlobalProductListSort sort,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) => sort == GlobalProductListSort.Default
            ? GetEnforcedLegalEntityScopePageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus,
                referenceableOnly, effectiveCandidateLegalEntityIds, serverNowUtc, cancellationToken)
            : throw new NotSupportedException("GLOBAL_PRODUCT_LIST_SORT_NOT_IMPLEMENTED");
    // MOD-0290-FU05: the register narrowed to one brand (null = every product, the overloads above).
    Task<GlobalProductPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        GlobalProductListSort sort,
        Guid? brandId,
        CancellationToken cancellationToken = default) => brandId is null
            ? GetPageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus, sort, cancellationToken)
            : throw new NotSupportedException("GLOBAL_PRODUCT_LIST_BRAND_FILTER_NOT_IMPLEMENTED");
    Task<GlobalProductPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        bool referenceableOnly,
        GlobalProductListSort sort,
        Guid? brandId,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) => brandId is null
            ? GetEnforcedLegalEntityScopePageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus,
                referenceableOnly, sort, effectiveCandidateLegalEntityIds, serverNowUtc, cancellationToken)
            : throw new NotSupportedException("GLOBAL_PRODUCT_LIST_BRAND_FILTER_NOT_IMPLEMENTED");
    /// <summary>WP-VW-W4 (W4-FIX-d, G-8) — the ids of the tenant's active global products whose NAME or CODE contains the text
    /// (<see cref="MdmSearchText"/>: case and Turkish I / İ insensitive).</summary>
    Task<IReadOnlyList<Guid>> FindIdsContainingAsync(string search, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
    Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct, CancellationToken cancellationToken = default);
    Task<GlobalProductLifecycleWriteResult> UpdateDraftAsync(
        Guid id,
        string globalProductName,
        string normalizedName,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product draft update is not implemented by this repository.");
    // MOD-0290-FU05 (T-4): the draft edit with its brand. brandSpecified = false leaves the stored brand as it is (the
    // edit above); true writes brandId (null = no brand) in the same conditional write as the name and the intent.
    Task<GlobalProductLifecycleWriteResult> UpdateDraftAsync(
        Guid id,
        string globalProductName,
        string normalizedName,
        bool brandSpecified,
        Guid? brandId,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => brandSpecified
            ? throw new NotSupportedException("Global Product draft brand update is not implemented by this repository.")
            : UpdateDraftAsync(id, globalProductName, normalizedName, expectedVersion, auditIntent, cancellationToken);
    Task<GlobalProductLifecycleWriteResult> SubmitIdentityAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowBinding workflowBinding,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product lifecycle submit is not implemented by this repository.");
    Task<GlobalProductLifecycleWriteResult> ReconcileIdentityDecisionAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowDecisionEvidence decisionEvidence,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product lifecycle decision reconciliation is not implemented by this repository.");
    Task<GlobalProductLifecycleWriteResult> WithdrawIdentityApprovalAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product identity approval withdrawal is not implemented by this repository.");
    Task<GlobalProductLifecycleWriteResult> RetireIdentityAsync(
        Guid id,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product retirement is not implemented by this repository.");
    Task<GlobalProductLifecycleWriteResult> ApplyRetirementDecisionAsync(
        Guid id,
        int expectedVersion,
        GlobalProductActiveLifecycleOperationBinding binding,
        bool approved,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product retirement workflow application is not implemented.");
    Task<GlobalProductLifecycleWriteResult> AcquireLifecycleOperationAsync(
        Guid id,
        int expectedVersion,
        GlobalProductActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product lifecycle operation admission is not implemented.");
    Task<GlobalProductLifecycleWriteResult> ApplyCorrectionDecisionAsync(
        Guid id,
        int expectedVersion,
        GlobalProductActiveLifecycleOperationBinding binding,
        string? approvedName,
        string? approvedNormalizedName,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product correction application is not implemented.");
    // BL-552: the identity approval closed at the engine without a decision that can be applied (cancelled in the Task
    // Center, timed out, decided by its own maker): the pending product is a draft again, in the same write as the
    // intent that says why (GlobalProductIdentityDecisionNotApplicable). Only the approval the product is bound to.
    Task<GlobalProductLifecycleWriteResult> ReturnIdentityToDraftAsync(
        Guid id,
        int expectedVersion,
        Guid workflowInstanceId,
        Guid approvalTaskId,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product identity return to draft is not implemented.");
    // BL-552: a correction / retirement request that ends without its decision applied gives the Global Product's
    // lock back in the same write as the intent that says why (…ManualReconciliationRequired).
    Task<GlobalProductLifecycleWriteResult> ReleaseLifecycleOperationAsync(
        Guid id,
        int expectedVersion,
        GlobalProductActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product lifecycle operation release is not implemented.");
    Task<ProductChildCreationAdmissionResult> AcquireChildCreationAdmissionAsync(
        Guid id,
        string creationCommandId,
        string requestFingerprint,
        DateTimeOffset acquiredAtUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product child admission is not implemented by this repository.");
    Task<ProductChildCreationAdmissionResult> CompleteChildCreationAdmissionAsync(
        Guid id,
        string creationCommandId,
        string requestFingerprint,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product child admission completion is not implemented by this repository.");
    Task<GlobalProductScopeCompletenessInventory> GetProductLegalEntityScopeCompletenessInventoryAsync(
        DateTimeOffset serverNowUtc,
        int maximumMissingItems,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Bounded product-scope inventory is not implemented by this repository.");
}

public sealed record GlobalProductPage(IReadOnlyList<GlobalProduct> Items, long TotalCount);
public sealed record GlobalProductScopeCompletenessInventory(
    long EligibleGlobalProductCount,
    long ConfiguredGlobalProductCount,
    IReadOnlyList<Guid> MissingGlobalProductIds);

public sealed record GlobalProductLifecycleWriteResult(
    bool Succeeded,
    GlobalProduct? GlobalProduct,
    string? ErrorCode = null,
    bool IsReplay = false);

public sealed record ProductChildCreationAdmissionResult(
    bool Succeeded,
    GlobalProduct? GlobalProduct,
    string? ErrorCode = null,
    bool IsReplay = false);
