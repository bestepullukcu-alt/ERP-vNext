using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// MOD-0290-FU04 S5 (D-16) — the ONE definition the stock-flag gate's replay exemption uses, shared by the GSKU and the
/// LSKU gate: the checkpoints at which a maker's repeat of a start can no longer be continued (the processor answers the
/// stored result or refuses — no new work). Measured against the processors: exactly "ended" (Completed,
/// AbandonedBeforeWorkflowStart, Superseded, DecisionNotApplicable) plus WithdrawalApplied. Every other checkpoint a Draft
/// record can hold is continued or reconciled by the repeat (AwaitingMakerReplay, ManualReconciliationRequired, the draft
/// restorations …), so the gate lets it through.
/// <para>NOT the same question as the stock-edit fence / "unfinished" queries (WithdrawalApplied still holds the record
/// there) or worker discovery (AwaitingMakerReplay / ManualReconciliationRequired are terminal for the worker). Those keep
/// their own sets; unifying them would change behaviour (S5 report, D-16).</para>
/// </summary>
public static class IdentityStartReplay
{
    public static bool CannotContinue(FirstGskuIdentityWorkflowCheckpoint checkpoint) => checkpoint is
        FirstGskuIdentityWorkflowCheckpoint.Completed
        or FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
        or FirstGskuIdentityWorkflowCheckpoint.Superseded
        or FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied
        or FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable;

    public static bool CannotContinue(LskuIdentityWorkflowCheckpoint checkpoint) => checkpoint is
        LskuIdentityWorkflowCheckpoint.Completed
        or LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
        or LskuIdentityWorkflowCheckpoint.Superseded
        or LskuIdentityWorkflowCheckpoint.WithdrawalApplied
        or LskuIdentityWorkflowCheckpoint.DecisionNotApplicable;
}
