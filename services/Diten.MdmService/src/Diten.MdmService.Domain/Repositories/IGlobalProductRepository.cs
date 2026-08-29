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
    Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct, CancellationToken cancellationToken = default);
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
    Task<GlobalProductLifecycleWriteResult> RetireIdentityAsync(
        Guid id,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Global Product retirement is not implemented by this repository.");
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
