namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// BL-552 (the GSKU FIX6 item 4 (b) rule on the Global Product's correction and retirement request): which STUCK
/// requests (manual reconciliation, the product's lock held) their requester's same command can settle — it reads the
/// engine again and retries the step — and which only a person can (BL-530, not built yet). Every stuck reason not on
/// this list (a write refusal that will be refused again in the same way, a name taken by another product, the limit
/// reached on an answer about the record) keeps the product locked until a person reconciles it; those are counted on
/// their own (needing_a_person) so an alert can be set on them.
/// </summary>
public static class GlobalProductLifecycleStuckKinds
{
    public static readonly IReadOnlyList<string> SettledByTheRequestersRepeat =
    [
        GlobalProductCorrectionWorkflowProcessor.RetryLimitReached,
        GlobalProductCorrectionWorkflowProcessor.EvidenceConflict,
        GlobalProductCorrectionWorkflowProcessor.StartEvidenceInvalid
    ];

    public static bool NeedsAPerson(string? code) => code is null || !SettledByTheRequestersRepeat.Contains(code);
}
