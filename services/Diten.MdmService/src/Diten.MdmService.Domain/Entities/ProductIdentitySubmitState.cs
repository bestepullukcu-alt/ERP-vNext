using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

/// <summary>Where a draft's latest identity submit stands — the same five answers for every product identity.</summary>
public enum ProductIdentitySubmitStateKind
{
    None,
    InFlight,
    ResubmitRequired,
    ReconciliationRequired,
    Ended
}

/// <summary>What a flow's checkpoint means for the rule below; each flow maps its own checkpoints onto these.</summary>
public enum ProductIdentitySubmitPhase
{
    /// <summary>The submit is finished (completed, or decided and closed).</summary>
    Finished,
    /// <summary>The submit ended before reaching the engine (refused, abandoned, superseded).</summary>
    EndedBeforeEngine,
    /// <summary>Waiting for its maker to submit again.</summary>
    AwaitingMakerReplay,
    /// <summary>Handed to manual reconciliation.</summary>
    ManualReconciliation,
    /// <summary>Anything else: on its way to, or at, the engine.</summary>
    Underway
}

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 item 6: the shared part of the in-flight rule GSKU got in FIX2 (FirstGskuIdentitySubmitState)
/// and Global Product gets now. A draft whose submit is on its way is not edited (the edit and the reservation are
/// atomic in each repository); a submit the engine never received is set aside by an edit; one that ended before the
/// engine at this very version cannot be repeated.
/// </summary>
public sealed record ProductIdentitySubmitState(ProductIdentitySubmitStateKind Kind, string? Code)
{
    public static readonly ProductIdentitySubmitState None = new(ProductIdentitySubmitStateKind.None, null);

    /// <param name="atThisVersion">The submit was made at the draft's current version.</param>
    /// <param name="engineMayHold">An approval exists, or a withdrawal was asked for: the engine holds something.</param>
    public static ProductIdentitySubmitState Classify(ProductIdentitySubmitPhase phase, bool atThisVersion,
        bool engineMayHold, string? code) => phase switch
    {
        ProductIdentitySubmitPhase.Finished => None,
        ProductIdentitySubmitPhase.EndedBeforeEngine => atThisVersion ? new(ProductIdentitySubmitStateKind.Ended, code) : None,
        ProductIdentitySubmitPhase.AwaitingMakerReplay when !engineMayHold => new(ProductIdentitySubmitStateKind.ResubmitRequired, code),
        ProductIdentitySubmitPhase.ManualReconciliation => new(ProductIdentitySubmitStateKind.ReconciliationRequired, code),
        _ => new(ProductIdentitySubmitStateKind.InFlight, null)
    };

    /// <summary>Global Product's checkpoints on the shared rule.</summary>
    public static ProductIdentitySubmitState Of(GlobalProductIdentityWorkflowOperation? latest, int currentProductVersion) =>
        latest is null ? None : Classify(latest.Checkpoint switch
            {
                GlobalProductIdentityWorkflowCheckpoint.Completed => ProductIdentitySubmitPhase.Finished,
                GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                    or GlobalProductIdentityWorkflowCheckpoint.Superseded => ProductIdentitySubmitPhase.EndedBeforeEngine,
                GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay => ProductIdentitySubmitPhase.AwaitingMakerReplay,
                GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired => ProductIdentitySubmitPhase.ManualReconciliation,
                _ => ProductIdentitySubmitPhase.Underway
            },
            latest.ExpectedProductVersion == currentProductVersion,
            latest.WorkflowInstanceId is not null || latest.WithdrawalCommandId is not null,
            latest.LastFailureCode);
}
