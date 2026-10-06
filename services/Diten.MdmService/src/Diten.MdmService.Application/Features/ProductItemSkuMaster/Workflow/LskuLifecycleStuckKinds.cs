namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// LSKU FIX1 S4 (the GSKU rule, GskuLifecycleStuckKinds): which STUCK LSKU retirement requests their requester's same
/// command can settle (the retry limit, evidence that contradicted itself, start evidence that could not be read) and
/// which only a person can (BL-530) — counted on their own by the needing_a_person gauge.
/// </summary>
public static class LskuLifecycleStuckKinds
{
    public static readonly IReadOnlyList<string> SettledByTheRequestersRepeat =
    [
        LskuRetirementRequestWorkflowProcessor.RetryLimitReached,
        LskuRetirementRequestWorkflowProcessor.EvidenceConflict,
        LskuRetirementRequestWorkflowProcessor.StartEvidenceInvalid
    ];

    public static bool NeedsAPerson(string? code) => code is null || !SettledByTheRequestersRepeat.Contains(code);

    // LSKU FIX2 item 3: the identity flow's — the requester's same submit re-reads the engine (ReconcileAsync) and
    // settles a provider contract mismatch, a start proof or evidence it could not read, a lookup the engine refused.
    // A limit reached on an answer about the record (STUCK_ON_RECORD_ANSWER) and any other cause are a person's.
    public static readonly IReadOnlyList<string> IdentitySettledByTheRequestersRepeat =
    [
        "REFERENCE_CONTRACT_MISMATCH",
        "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID",
        "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT"
    ];
}
