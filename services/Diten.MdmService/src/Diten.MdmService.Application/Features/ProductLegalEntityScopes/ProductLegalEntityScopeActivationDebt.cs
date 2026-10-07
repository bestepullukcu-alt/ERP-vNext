using System.Collections.Frozen;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 (5) — THE KNOWN GAPS, NAMED, IN PRODUCTION CODE: the product writers that take neither the
/// write fence nor a writer lease (S8), and the recovery runners that read neither the rollout, its fence nor its mode
/// (FIX2 (4)). While either list is non-empty an Enforced tenant would have writers the scope does not see, so the switch
/// to Enforced is REFUSED (CT decision, every environment, Development included). The contract tests read these same
/// lists: a new unguarded writer or unfenced runner turns them red, a guarded one must leave its list.
/// WP-MDM-SCOPE-INT-01 (measured): the runner list is EMPTY (every recovery runner reads the fence); the writer list is
/// NOT — sixteen lifecycle / workflow writers are still unguarded, so the gate stays SHUT. It opens only when that list
/// empties in its own work package (not scheduled in INT-01).
/// FIX3 (5): the lists are frozen — no code can empty them at run time to open the gate.
/// </summary>
public interface IProductLegalEntityScopeActivationDebt
{
    IReadOnlyCollection<string> UnguardedWriters { get; }
    IReadOnlyCollection<string> UnfencedRecoveryRunners { get; }
}

public sealed class ProductLegalEntityScopeActivationDebt : IProductLegalEntityScopeActivationDebt
{
    public const string ActivationBlockedCode = "PRODUCT_SCOPE_ACTIVATION_BLOCKED_UNGUARDED_WRITERS";

    public static ProductLegalEntityScopeActivationDebt Instance { get; } = new();

    /// <summary>GP / GSKU / LSKU lifecycle and workflow commands with no fence and no lease (FIX1 S8).</summary>
    public static readonly FrozenSet<string> KnownUnguardedWriters = new[]
    {
        "DispatchProductAbbreviationWorkItemActionCommand",
        "RetireGlobalProductIdentityCommand",
        "RetireGskuIdentityPairCommand",
        "RetireLskuIdentityCommand",
        "StartFirstGskuIdentityWorkflowCommand",
        "StartGlobalProductCorrectionWorkflowCommand",
        "StartGlobalProductIdentityWorkflowCommand",
        "StartGlobalProductRetirementRequestWorkflowCommand",
        "StartGskuCorrectionWorkflowCommand",
        "StartGskuRetirementRequestWorkflowCommand",
        "StartLskuIdentityWorkflowCommand",
        "StartLskuRetirementRequestWorkflowCommand",
        "UpdateGlobalProductDraftCommand",
        "WithdrawFirstGskuIdentityApprovalCommand",
        "WithdrawGlobalProductIdentityApprovalCommand",
        "WithdrawLskuIdentityApprovalCommand"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Recovery runners that neither take <c>ProductIdentityBackgroundScope</c> / <c>LskuBackgroundScope</c> nor read the
    /// fence: they may write during a fence and in a FailClosedSuspended tenant. Their files belong to the GP and LSKU
    /// rounds (FIX2 (4): not touched here).
    /// </summary>
    // WP-MDM-SCOPE-INT-01: the debt only shrinks — the three runners read the fence now (GP flow 1 FIX1 item 8, GP flow 2,
    // LSKU FIX1 B4: ProductIdentityBackgroundScope / LskuBackgroundScope) and left the list.
    public static readonly FrozenSet<string> KnownUnfencedRecoveryRunners =
        Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);

    public IReadOnlyCollection<string> UnguardedWriters => KnownUnguardedWriters;
    public IReadOnlyCollection<string> UnfencedRecoveryRunners => KnownUnfencedRecoveryRunners;

    /// <summary>True while any named debt is open — activation is then refused.</summary>
    public static bool BlocksActivation(IProductLegalEntityScopeActivationDebt debt)
        => debt.UnguardedWriters.Count > 0 || debt.UnfencedRecoveryRunners.Count > 0;
}
