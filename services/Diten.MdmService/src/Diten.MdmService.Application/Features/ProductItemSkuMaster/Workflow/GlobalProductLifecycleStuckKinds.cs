namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// BL-552 (the GSKU FIX6 item 4 (b) rule on the Global Product's correction and retirement request): which STUCK
/// requests (manual reconciliation, the product's lock held) their requester's same command can settle — it reads the
/// engine again and retries the step — and which only a person can (BL-530, not built yet). Every stuck reason not on
/// this list (a write refusal that will be refused again in the same way, the limit reached on an answer about the
/// record, a release that met a concurrent write) keeps the product locked until a person reconciles it; those are
/// counted on their own (needing_a_person) so an alert can be set on them. A name taken by another product after the
/// approval is NOT stuck: the request ENDS and gives the product back (BL-552 flow 2 FIX1).
/// </summary>
public static class GlobalProductLifecycleStuckKinds
{
    public static readonly IReadOnlyList<string> SettledByTheRequestersRepeat =
    [
        GlobalProductCorrectionWorkflowProcessor.RetryLimitReached,
        GlobalProductCorrectionWorkflowProcessor.EvidenceConflict,
        GlobalProductCorrectionWorkflowProcessor.StartEvidenceInvalid,
        GlobalProductRetirementRequestWorkflowProcessor.RetryLimitReached,
        GlobalProductRetirementRequestWorkflowProcessor.EvidenceConflict,
        GlobalProductRetirementRequestWorkflowProcessor.StartEvidenceInvalid
    ];

    public static bool NeedsAPerson(string? code) => code is null || !SettledByTheRequestersRepeat.Contains(code);
}
