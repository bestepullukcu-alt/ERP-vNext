using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IFinishedGoodRepository
{
    Task<FinishedGoodCreationAttemptResult> BindCreationAttemptAsync(
        Guid gskuId,
        string normalizedCreationCommandId,
        string requestFingerprint,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FINISHED_GOOD_CREATION_ATTEMPT_NOT_IMPLEMENTED");
    Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinishedGood?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default);
    Task<FinishedGood?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<FinishedGoodPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        CancellationToken cancellationToken = default);
    Task<FinishedGoodPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FINISHED_GOOD_LEGAL_ENTITY_SCOPE_READ_CONTRACT_NOT_IMPLEMENTED");
    Task<FinishedGoodCreateResult> CreateDraftAsync(
        FinishedGood finishedGood,
        CancellationToken cancellationToken = default);
    Task<FinishedGoodCreateResult> CreateDraftWithAdmissionAsync(
        FinishedGood finishedGood, string admissionFingerprint,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GSKU_CHILD_ADMISSION_NOT_IMPLEMENTED");

    // FG FIX1 item 1: the audited Draft cancellation — one CAS writes the decision and its intent; the FG is retained.
    Task<FinishedGoodDraftCancelResult> CancelDraftAsync(
        Guid id, int expectedVersion, FinishedGoodDraftCancellation cancellation, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FINISHED_GOOD_DRAFT_CANCEL_NOT_IMPLEMENTED");
}

public sealed record FinishedGoodDraftCancelResult(
    bool Succeeded,
    FinishedGood? FinishedGood,
    string? ErrorCode = null,
    bool IsReplay = false);

public sealed record FinishedGoodPage(IReadOnlyList<FinishedGood> Items, long TotalCount);
public sealed record FinishedGoodCreateResult(
    bool Succeeded,
    FinishedGood? FinishedGood,
    string? ErrorCode = null,
    bool WriteOutcomeAmbiguous = false);
