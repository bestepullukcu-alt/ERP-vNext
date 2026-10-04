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

/// <summary>FIX2 A1 — every decision on an entry, written with its history row and audit intent in ONE transaction.</summary>
public enum ProductAbbreviationDecisionKind
{
    Approve,
    Reject,
    Cancel,
    RequestRetirement,
    ApproveRetirement,
    RejectRetirement
}

public sealed record ProductAbbreviationDecisionEvidence(
    ProductAbbreviationHistoryEntry History,
    LocalAuditIntent Intent);

/// <summary>
/// One decision. <see cref="BuildEvidence"/> receives the entry as it will read after the decision. A decision already
/// written under the same key is "mine" only when its stored decider is <see cref="ActorSubjectId"/>; otherwise the answer
/// is ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER, never a success.
/// </summary>
public sealed record ProductAbbreviationDecision(
    ProductAbbreviationDecisionKind Kind,
    Guid EntryId,
    int ExpectedVersion,
    string ActorSubjectId,
    string IdempotencyKey,
    string? Reason,
    DateTimeOffset DecidedAtUtc,
    string? RetirementRequestId,
    Func<ProductAbbreviationRegisterEntry, ProductAbbreviationDecisionEvidence> BuildEvidence);

/// <summary>
/// FIX2 A2 / item 6 — a new REQUESTED entry (an allocation or a correction), inserted with its history row and audit
/// intent in ONE transaction. A correction claims the code it replaces in the same transaction; an allocation touches
/// the product's slot, so two requests for one product cannot both pass the "nothing pending" check.
/// </summary>
public sealed record ProductAbbreviationRequestWrite(
    ProductAbbreviationRegisterEntry Entry,
    Func<ProductAbbreviationRegisterEntry, ProductAbbreviationDecisionEvidence> BuildEvidence);

/// <summary>FIX2 A3 — where the previous page of pending work ended: its order value and id.</summary>
public sealed record ProductAbbreviationPendingCursor(long PendingSinceUtcTicks, Guid Id);

public interface IProductAbbreviationRegisterRepository
{
    Task<ProductAbbreviationRegisterWriteResult> DecideAsync(
        ProductAbbreviationDecision decision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Decisions are not supported by this repository.");
    Task<ProductAbbreviationRegisterWriteResult> RequestEntryAsync(
        ProductAbbreviationRequestWrite write,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Requests are not supported by this repository.");
    Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Batch reads are not supported by this repository.");
    /// <summary>
    /// One page of pending work in the order it started waiting (FIX2 A3), strictly after <paramref name="after"/>.
    /// </summary>
    Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsPageAsync(
        ProductAbbreviationPendingCursor? after,
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
