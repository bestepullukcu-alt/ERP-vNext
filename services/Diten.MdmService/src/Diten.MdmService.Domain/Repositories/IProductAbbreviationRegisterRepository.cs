using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record ProductAbbreviationRegisterWriteResult(
    bool Succeeded,
    ProductAbbreviationRegisterEntry? Entry,
    string? ErrorCode = null,
    bool IsReplay = false,
    bool ReconciliationRequired = false);

/// <summary>
/// FIX1 B1 — everything a correction approval writes, decided before the write and applied in ONE transaction:
/// the former entry retires, the replacement activates, and both history rows and both audit intents land with
/// them, or nothing does. <see cref="BuildEvidence"/> receives the two entries as they will read after the write.
/// </summary>
public sealed record ProductAbbreviationCorrectionApproval(
    Guid FormerEntryId,
    int ExpectedFormerVersion,
    Guid ReplacementEntryId,
    int ExpectedReplacementVersion,
    string CheckerSubjectId,
    string IdempotencyKey,
    string? Reason,
    DateTimeOffset DecidedAtUtc,
    Func<ProductAbbreviationRegisterEntry, ProductAbbreviationRegisterEntry, ProductAbbreviationCorrectionEvidence> BuildEvidence);

public sealed record ProductAbbreviationCorrectionEvidence(
    ProductAbbreviationHistoryEntry FormerHistory,
    LocalAuditIntent FormerIntent,
    ProductAbbreviationHistoryEntry ReplacementHistory,
    LocalAuditIntent ReplacementIntent);

public interface IProductAbbreviationRegisterRepository
{
    /// <summary>FIX1 S8 — one page of pending work, in id order, strictly after <paramref name="afterId"/>.</summary>
    Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsPageAsync(
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Pending work-item paging is not supported by this repository.");
    /// <summary>FIX1 B2 — every entry of one Global Product, whatever its status.</summary>
    Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Listing a product's entries is not supported by this repository.");
    /// <summary>FIX1 S2 — does the product already hold an ACTIVE or REQUESTED entry other than <paramref name="exceptEntryId"/>?</summary>
    Task<bool> HasOpenEntryForProductAsync(
        Guid globalProductId,
        Guid exceptEntryId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Open-entry lookup is not supported by this repository.");
    /// <summary>
    /// FIX1 B3 — marks an ACTIVE entry as claimed by <paramref name="correctionEntryId"/>, only while it has no
    /// retirement request and its claim is still <paramref name="observedClaim"/>. True when the claim is this one.
    /// </summary>
    Task<bool> ClaimPendingCorrectionAsync(
        Guid formerEntryId,
        Guid? observedClaim,
        Guid correctionEntryId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Correction claims are not supported by this repository.");
    /// <summary>FIX1 B1 — see <see cref="ProductAbbreviationCorrectionApproval"/>.</summary>
    Task<ProductAbbreviationRegisterWriteResult> ApproveCorrectionAsync(
        ProductAbbreviationCorrectionApproval approval,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Correction approval is not supported by this repository.");
    Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Pending work-item projection is not supported by this repository.");
    Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetInitialPendingWorkItemsAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Pending work-item projection is not supported by this repository.");
    Task<ProductAbbreviationRegisterEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterEntry?> GetByAllocationIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterEntry?> GetActiveByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterEntry?> ResolveActiveAsync(string normalizedAbbreviation, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> InsertRequestedAsync(ProductAbbreviationRegisterEntry entry, CancellationToken cancellationToken = default);
    Task<bool> AppendAuditIntentIfAbsentAsync(
        Guid id,
        LocalAuditIntent intent,
        CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> TransitionAsync(
        Guid id,
        int expectedVersion,
        ProductAbbreviationLifecycleStatus expectedStatus,
        ProductAbbreviationLifecycleStatus targetStatus,
        string decisionActor,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> RequestRetirementAsync(
        Guid id,
        int expectedVersion,
        string retirementRequestId,
        string makerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default,
        Guid? observedPendingCorrection = null);
    Task<ProductAbbreviationRegisterWriteResult> ClearRetirementRequestAsync(
        Guid id,
        int expectedVersion,
        string retirementRequestId,
        string checkerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default);
}
