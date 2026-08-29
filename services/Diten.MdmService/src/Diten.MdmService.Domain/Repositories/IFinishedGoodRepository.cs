using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IFinishedGoodRepository
{
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

    Task<FinishedGoodLifecycleWriteResult> SubmitIdentityAsync(
        Guid id, int expectedVersion, ProductIdentityWorkflowBinding workflowBinding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FINISHED_GOOD_LIFECYCLE_SUBMIT_NOT_IMPLEMENTED");

    Task<FinishedGoodLifecycleWriteResult> ReconcileIdentityDecisionAsync(
        Guid id, int expectedVersion, ProductIdentityWorkflowBinding expectedWorkflowBinding,
        ProductIdentityWorkflowDecisionEvidence decisionEvidence,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FINISHED_GOOD_LIFECYCLE_DECISION_NOT_IMPLEMENTED");

    Task<FinishedGoodLifecycleWriteResult> RetireIdentityAsync(
        Guid id, int expectedVersion, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FINISHED_GOOD_LIFECYCLE_RETIRE_NOT_IMPLEMENTED");
}

public sealed record FinishedGoodPage(IReadOnlyList<FinishedGood> Items, long TotalCount);
public sealed record FinishedGoodCreateResult(
    bool Succeeded,
    FinishedGood? FinishedGood,
    string? ErrorCode = null,
    bool WriteOutcomeAmbiguous = false);

public sealed record FinishedGoodLifecycleWriteResult(
    bool Succeeded,
    FinishedGood? FinishedGood,
    string? ErrorCode = null,
    bool IsReplay = false);
