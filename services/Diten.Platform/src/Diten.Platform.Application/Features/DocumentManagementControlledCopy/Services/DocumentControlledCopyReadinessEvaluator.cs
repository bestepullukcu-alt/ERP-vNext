using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Services;

/// <summary>
/// MOD-0029-FU17 — pure controlled-copy withdrawal readiness evaluator (GMG-QMS-SOP-0001 §9.13, §19 gate 6). Given an
/// entry's copies, its open withdrawal plan and its obsolete findings, it decides whether superseded copies are under
/// control: every withdrawal-required copy is withdrawn/reconciled/destroyed, any open plan is Completed, and there is
/// no open critical obsolete finding. No I/O.
/// </summary>
public sealed class DocumentControlledCopyReadinessEvaluator
{
    public CopyWithdrawalReadinessModel Evaluate(
        Guid registerEntryId,
        IReadOnlyList<DocumentControlledCopy> copies,
        DocumentCopyWithdrawalPlan? openPlan,
        IReadOnlyList<DocumentObsoleteCopyFinding> findings)
    {
        var blocking = new List<string>();

        var pending = copies.Where(c => c.WithdrawalRequired && !IsAccountedFor(c.CopyStatus)).ToList();
        if (pending.Count > 0)
        {
            blocking.Add($"{pending.Count} controlled copy/copies require withdrawal but are not withdrawn/reconciled.");
        }

        if (openPlan is not null && openPlan.PlanStatus != CopyWithdrawalPlanStatus.Completed)
        {
            blocking.Add($"A withdrawal plan is {openPlan.PlanStatus}, not Completed.");
        }

        var openCritical = findings.Count(f => f.Status == ObsoleteCopyFindingStatus.Open && f.Severity == ObsoleteCopyFindingSeverity.Critical);
        if (openCritical > 0)
        {
            blocking.Add($"{openCritical} open critical obsolete-copy finding(s) exist.");
        }

        var hasData = copies.Count > 0 || openPlan is not null || findings.Count > 0;
        var ready = hasData && blocking.Count == 0;

        return new CopyWithdrawalReadinessModel(
            registerEntryId,
            ready,
            hasData,
            copies.Count(c => c.CopyStatus == ControlledCopyStatus.Active),
            copies.Count(c => c.CopyStatus == ControlledCopyStatus.PendingWithdrawal),
            copies.Count(c => c.CopyStatus is ControlledCopyStatus.Withdrawn or ControlledCopyStatus.Reconciled),
            copies.Count(c => c.CopyStatus == ControlledCopyStatus.Obsolete),
            openCritical,
            openPlan?.PlanStatus.ToString(),
            blocking);
    }

    /// <summary>A withdrawal-required copy is accounted for once it is withdrawn, reconciled or destroyed.</summary>
    private static bool IsAccountedFor(ControlledCopyStatus status) =>
        status is ControlledCopyStatus.Withdrawn or ControlledCopyStatus.Reconciled or ControlledCopyStatus.Destroyed;
}
