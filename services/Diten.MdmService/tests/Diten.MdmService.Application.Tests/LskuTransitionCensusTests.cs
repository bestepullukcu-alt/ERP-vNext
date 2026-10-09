using System.Text.RegularExpressions;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX2 item 2 (b): each LSKU operation store allows EXACTLY the (claimed checkpoint → next)
/// pairs its processor writes — no more, no fewer. The census below was collected from the processors' source (every
/// write site traced back to the switch arm that claims it; the line references are the 2026-10-06 FIX2 tree) and is
/// held against the stores' tables over every pair of the enum, so adding or removing a pair in a table turns this red.
/// A source scan adds the mechanical half: a new write target in a processor that the census does not know turns it red.
/// Same-checkpoint writes (a retry, a lease release) are always allowed and are not listed.
/// </summary>
public sealed class LskuTransitionCensusTests
{
    private const LskuIdentityWorkflowCheckpoint Prepared = LskuIdentityWorkflowCheckpoint.Prepared;
    private const LskuIdentityWorkflowCheckpoint Sou = LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown;
    private const LskuIdentityWorkflowCheckpoint Amr = LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay;
    private const LskuIdentityWorkflowCheckpoint Ws = LskuIdentityWorkflowCheckpoint.WorkflowStarted;
    private const LskuIdentityWorkflowCheckpoint Lpa = LskuIdentityWorkflowCheckpoint.LocalPendingApplied;
    private const LskuIdentityWorkflowCheckpoint Ad = LskuIdentityWorkflowCheckpoint.AwaitingDecision;
    private const LskuIdentityWorkflowCheckpoint Do = LskuIdentityWorkflowCheckpoint.DecisionObserved;
    private const LskuIdentityWorkflowCheckpoint Av = LskuIdentityWorkflowCheckpoint.ApprovalValidated;
    private const LskuIdentityWorkflowCheckpoint Da = LskuIdentityWorkflowCheckpoint.DecisionApplied;
    private const LskuIdentityWorkflowCheckpoint Dna = LskuIdentityWorkflowCheckpoint.DecisionNotApplicable;
    private const LskuIdentityWorkflowCheckpoint Abandoned = LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart;
    private const LskuIdentityWorkflowCheckpoint Manual = LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired;
    private const LskuIdentityWorkflowCheckpoint Completed = LskuIdentityWorkflowCheckpoint.Completed;
    private const LskuIdentityWorkflowCheckpoint Wr = LskuIdentityWorkflowCheckpoint.WithdrawalRequested;
    private const LskuIdentityWorkflowCheckpoint Wpo = LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved;
    private const LskuIdentityWorkflowCheckpoint Wou = LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown;
    private const LskuIdentityWorkflowCheckpoint Wo = LskuIdentityWorkflowCheckpoint.WithdrawalObserved;
    private const LskuIdentityWorkflowCheckpoint Wa = LskuIdentityWorkflowCheckpoint.WithdrawalApplied;

    // LskuIdentityWorkflowProcessor: ProcessAsync (submit arms), ProcessWithdrawalAsync (withdrawal arms),
    // CloseForDraftCancelAsync (AMR only); the repository's own writers (EndWithDraftAsync: Abandoned / DNA,
    // ApplyWithdrawalAsync: WO → WA) included.
    internal static readonly HashSet<(LskuIdentityWorkflowCheckpoint, LskuIdentityWorkflowCheckpoint)> IdentityCensus =
    [
        (Prepared, Sou),                                                   // Prepared arm
        (Sou, Ws), (Sou, Amr), (Sou, Abandoned), (Sou, Manual),            // ResolveStartAsync
        (Amr, Ws), (Amr, Sou), (Amr, Abandoned), (Amr, Manual),            // ResolveStartAsync, CloseForDraftCancelAsync
        (Amr, Wou), (Amr, Wo), (Amr, Ad),                                  // the withdrawal's maker replay (item 2)
        (Ws, Lpa), (Ws, Manual),                                           // ApplyLocalPendingAsync
        (Lpa, Ad),
        (Ad, Do), (Ad, Dna), (Ad, Manual), (Ad, Wr),                       // ObserveDecisionAsync; the withdraw command
        (Do, Av), (Do, Da), (Do, Dna), (Do, Manual),                       // ValidateApprovalOrApplyRejectAsync
        (Av, Da), (Av, Dna), (Av, Manual),                                 // ApplyDecisionAsync
        (Da, Completed), (Da, Manual),                                     // VerifyDecisionAppliedAndCompleteAsync
        (Manual, Do), (Manual, Ws), (Manual, Abandoned), (Manual, Ad), (Manual, Da), // ReconcileAsync
        (Wr, Wpo), (Wr, Ad),                                               // preflight; ClosedBeforeWithdrawalAsync
        (Wpo, Wou), (Wpo, Wo), (Wpo, Ad), (Wpo, Amr),                      // the cancel (with / without a token)
        (Wou, Wo), (Wou, Ad), (Wou, Amr),
        (Wo, Wa), (Wo, Manual),                                            // ApplyWithdrawalAsync; evidence invalid
        (Wa, Completed)
    ];

    // LskuRetirementRequestWorkflowProcessor (RetryAsync's Manual remap: Manual → AD with an instance, → SOU without).
    // AwaitingMakerReplay is never written; older records leave it through the AMR rows.
    internal static readonly HashSet<(LskuRetirementRequestCheckpoint, LskuRetirementRequestCheckpoint)> RetirementCensus =
    [
        (LskuRetirementRequestCheckpoint.Prepared, LskuRetirementRequestCheckpoint.WorkflowStarted),
        (LskuRetirementRequestCheckpoint.Prepared, LskuRetirementRequestCheckpoint.StartOutcomeUnknown),
        (LskuRetirementRequestCheckpoint.Prepared, LskuRetirementRequestCheckpoint.ManualReconciliationRequired),
        (LskuRetirementRequestCheckpoint.StartOutcomeUnknown, LskuRetirementRequestCheckpoint.WorkflowStarted),
        (LskuRetirementRequestCheckpoint.StartOutcomeUnknown, LskuRetirementRequestCheckpoint.ManualReconciliationRequired),
        (LskuRetirementRequestCheckpoint.AwaitingMakerReplay, LskuRetirementRequestCheckpoint.WorkflowStarted),
        (LskuRetirementRequestCheckpoint.AwaitingMakerReplay, LskuRetirementRequestCheckpoint.StartOutcomeUnknown),
        (LskuRetirementRequestCheckpoint.AwaitingMakerReplay, LskuRetirementRequestCheckpoint.ManualReconciliationRequired),
        (LskuRetirementRequestCheckpoint.WorkflowStarted, LskuRetirementRequestCheckpoint.AwaitingDecision),
        (LskuRetirementRequestCheckpoint.AwaitingDecision, LskuRetirementRequestCheckpoint.DecisionObserved),
        (LskuRetirementRequestCheckpoint.AwaitingDecision, LskuRetirementRequestCheckpoint.ManualReconciliationRequired),
        (LskuRetirementRequestCheckpoint.DecisionObserved, LskuRetirementRequestCheckpoint.DecisionApplied),
        (LskuRetirementRequestCheckpoint.DecisionObserved, LskuRetirementRequestCheckpoint.ManualReconciliationRequired),
        (LskuRetirementRequestCheckpoint.DecisionApplied, LskuRetirementRequestCheckpoint.Completed),
        (LskuRetirementRequestCheckpoint.DecisionApplied, LskuRetirementRequestCheckpoint.ManualReconciliationRequired),
        (LskuRetirementRequestCheckpoint.ManualReconciliationRequired, LskuRetirementRequestCheckpoint.WorkflowStarted),
        (LskuRetirementRequestCheckpoint.ManualReconciliationRequired, LskuRetirementRequestCheckpoint.AwaitingDecision),
        (LskuRetirementRequestCheckpoint.ManualReconciliationRequired, LskuRetirementRequestCheckpoint.DecisionObserved),
        (LskuRetirementRequestCheckpoint.ManualReconciliationRequired, LskuRetirementRequestCheckpoint.StartOutcomeUnknown)
    ];

    [Fact]
    public void The_identity_store_allows_exactly_the_transitions_its_processor_makes()
    {
        var all = Enum.GetValues<LskuIdentityWorkflowCheckpoint>();
        var wrong = (from a in all from b in all where a != b
                     let allowed = LskuIdentityWorkflowOperationRepository.IsAllowedTransition(a, b)
                     where allowed != IdentityCensus.Contains((a, b))
                     select $"{a}→{b} store={allowed}").ToList();

        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    [Fact]
    public void The_retirement_request_store_allows_exactly_the_transitions_its_processor_makes()
    {
        var all = Enum.GetValues<LskuRetirementRequestCheckpoint>();
        var wrong = (from a in all from b in all where a != b
                     let allowed = LskuRetirementRequestOperationRepository.Allowed(a, b)
                     where allowed != RetirementCensus.Contains((a, b))
                     select $"{a}→{b} store={allowed}").ToList();

        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    // The mechanical half: every checkpoint a processor names as the target of a write is a target in its census.
    [Theory]
    [InlineData("LskuIdentityWorkflowProcessor.cs", "LskuIdentityWorkflowCheckpoint")]
    [InlineData("LskuRetirementRequestWorkflowProcessor.cs", "LskuRetirementRequestCheckpoint")]
    public void Every_write_target_named_in_a_processor_is_in_its_census(string file, string enumName)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src",
            "Diten.MdmService.Application", "Features", "ProductItemSkuMaster", "Workflow", file));
        var targets = Regex.Matches(source,
                @"(?:AdvanceAsync\(\s*claim,|ScheduleRetryAsync\(\s*claim,|Advance\(\s*claim,|RetryAsync\(\s*op,\s*claim,|new\()\s*"
                + enumName + @"\.(\w+)")
            .Select(m => m.Groups[1].Value).ToHashSet();
        var known = enumName == "LskuIdentityWorkflowCheckpoint"
            ? IdentityCensus.Select(x => x.Item2.ToString()).ToHashSet()
            : RetirementCensus.Select(x => x.Item2.ToString()).ToHashSet();

        Assert.NotEmpty(targets);
        Assert.Empty(targets.Except(known));
    }

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }
}
