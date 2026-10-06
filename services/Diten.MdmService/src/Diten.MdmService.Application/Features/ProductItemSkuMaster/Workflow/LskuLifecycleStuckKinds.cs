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
}
