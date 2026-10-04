using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

/// <summary>Where a draft GSKU's latest identity submit stands, for the draft's own actions (edit, submit) and screen.</summary>
public enum FirstGskuIdentitySubmitStateKind
{
    /// <summary>No submit, or the latest one is finished (completed, not applicable): edit and submit are open.</summary>
    None,
    /// <summary>The submit is on its way to, or at, the approval engine: the draft waits; the worker carries it on.</summary>
    InFlight,
    /// <summary>The engine never received the submit: the maker submits again (the same submit) or edits the draft.</summary>
    ResubmitRequired,
    /// <summary>The engine may hold an approval MDM cannot account for: the submitter submits again, which reads the
    /// engine and reconciles; until then the draft stays as submitted.</summary>
    ReconciliationRequired,
    /// <summary>The submit ended before reaching the engine (refused there, or replaced by an edit) at THIS version:
    /// the same submit cannot be repeated — the draft is edited and submitted anew.</summary>
    Ended
}

public sealed record FirstGskuIdentitySubmitState(FirstGskuIdentitySubmitStateKind Kind, string? Code)
{
    public static readonly FirstGskuIdentitySubmitState None = new(FirstGskuIdentitySubmitStateKind.None, null);

    // FIX3 E2: the GSKU version the submit was made at — a reconciling repeat is the SAME submit, so it carries this
    // version, not the GSKU's current one (which moved when the pair went pending).
    public int? SubmittedAtVersion { get; init; }
    // FIX3 E2: the submit is in a withdrawal: its way out is the requester's repeat of that withdrawal, not a submit.
    public bool InWithdrawal { get; init; }

    /// <summary>
    /// The one rule (WP-MDM-GSKU-ACCEPT-01 FIX2 item 1 — "no state without a way out"). FIX1 counted every non-final
    /// checkpoint as "unfinished", so a submit that never reached the engine, or one in manual reconciliation, froze
    /// the draft for good: edit and submit hidden, the edit refused, nothing ever moving it on.
    /// </summary>
    public static FirstGskuIdentitySubmitState Of(FirstGskuIdentityWorkflowOperation? latest, int currentGskuVersion)
    {
        if (latest is null) return None;
        // FIX3 item 6: the rule itself is shared with Global Product (ProductIdentitySubmitState); the kinds line up.
        var shared = ProductIdentitySubmitState.Classify(latest.Checkpoint switch
            {
                FirstGskuIdentityWorkflowCheckpoint.Completed or FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                    => ProductIdentitySubmitPhase.Finished,
                FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart or FirstGskuIdentityWorkflowCheckpoint.Superseded
                    => ProductIdentitySubmitPhase.EndedBeforeEngine,
                FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay => ProductIdentitySubmitPhase.AwaitingMakerReplay,
                FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired => ProductIdentitySubmitPhase.ManualReconciliation,
                _ => ProductIdentitySubmitPhase.Underway
            },
            latest.ExpectedGskuVersion == currentGskuVersion,
            latest.WithdrawalCommandId is not null || latest.WorkflowInstanceId is not null,
            latest.LastFailureCode);
        return shared.Kind == ProductIdentitySubmitStateKind.None
            ? None
            : new((FirstGskuIdentitySubmitStateKind)(int)shared.Kind, shared.Code)
            {
                SubmittedAtVersion = latest.ExpectedGskuVersion,
                InWithdrawal = latest.WithdrawalCommandId is not null
            };
    }
}
