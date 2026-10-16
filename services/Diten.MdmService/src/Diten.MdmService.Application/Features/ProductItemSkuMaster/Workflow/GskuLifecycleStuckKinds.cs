namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX6 item 4 (b): which STUCK correction / retirement requests (manual reconciliation, the GSKU's
/// lock held) their requester's same command can settle, and which only a person can (BL-530, not built yet).
/// The requester's repeat reads the engine again (ReconcileAsync) and retries the step: it settles a request stuck on
/// the retry limit, on evidence that contradicted itself, or on a start whose evidence could not be read. Every other
/// stuck reason — a write refusal that will be refused again in the same way (an idempotency or contract conflict, a
/// lock that is not the request's, a GSKU that is gone, the GSKU drifted, the admission refused, the pair retirement
/// quarantined) — keeps the GSKU locked until a person reconciles it. Those are counted on their own so an alert can
/// be set on them.
/// FIX7 item 2: a limit reached on an answer about the record (the engine's, the store's) is *_STUCK_ON_RECORD_ANSWER /
/// *_STUCK_ON_RELEASE_CONFLICT — not on the list below, so a person's. RETRY_LIMIT_REACHED is now written only for a
/// service fault, which keeps retrying (never in manual reconciliation) and heals by itself.
/// </summary>
public static class GskuLifecycleStuckKinds
{
    public static readonly IReadOnlyList<string> SettledByTheRequestersRepeat =
    [
        GskuCorrectionWorkflowProcessor.RetryLimitReached,
        GskuCorrectionWorkflowProcessor.EvidenceConflict,
        GskuCorrectionWorkflowProcessor.StartEvidenceInvalid,
        GskuRetirementRequestWorkflowProcessor.RetryLimitReached,
        GskuRetirementRequestWorkflowProcessor.EvidenceConflict,
        GskuRetirementRequestWorkflowProcessor.StartEvidenceInvalid
    ];

    public static bool NeedsAPerson(string? code) => code is null || !SettledByTheRequestersRepeat.Contains(code);
}
