using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Application.Features.Workflow;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Recovery;

/// <summary>
/// BL-540 (1) — THE ORPHAN RECOVERY. FIX1 (CT decision D): no HTTP endpoint and no permission key — it runs only as the
/// operator's one-shot command on the server (<see cref="WorkflowOrphanRecoveryCommandLine"/>, `--run-workflow-orphan-recovery`);
/// the right to run it is access to the server and its environment.
/// An orphan is an approval or review the Task Center no longer owns and nobody reads, yet it stays live and decidable (and
/// the SLA sweep escalates it):
/// <list type="bullet">
/// <item><c>TASK_DELETED</c> — its task is gone (deleted before D2-FIX2/3/4 started withdrawing approvals with the task);</item>
/// <item><c>REVIEW_ROUND_REPLACED</c> — a review whose task now links a later round, and which is not the latest round
/// either (a hand-over overwrote the link of round 1 before D2-FIX3).</item>
/// </list>
/// <para>DRY RUN by default (<paramref name="Apply"/> false): counts and lists (ids, age, reason — no personal data) and writes
/// nothing. With <paramref name="Apply"/> each orphan's open approval task is cancelled through the engine's own cancel
/// command (<see cref="CancelWorkflowTaskCommand"/>; its log, completion event and audit row), under the recovery's
/// system actor. Tenant-scoped (the caller's tenant). Upward requests (BL-540 (2)) are not looked at.</para>
/// </summary>
/// <param name="After">FIX1 item 5 — resume after this instance id (the <see cref="WorkflowOrphanSweepResult.NextAfter"/> of the
/// previous call): the scan walks EVERY open instance page by page; a call stops only when it has found
/// <paramref name="MaxItems"/> orphans, and then says where to go on.</param>
/// <param name="OnlyIds">FIX2 K1 — with <paramref name="Apply"/>, close ONLY these (the ids the operator's dry run counted
/// and <c>--expect</c> checked); an orphan that appeared between the two scans is left for the next run.</param>
public sealed record SweepWorkflowOrphansCommand(
    bool Apply,
    int MaxItems,
    string CorrelationId,
    Guid? After = null,
    string? Tool = null,
    string? Host = null,
    IReadOnlyCollection<Guid>? OnlyIds = null) : IRequest<Response<WorkflowOrphanSweepResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public const string SystemActor = "system:workflow-orphan-recovery";
    public const string TaskDeleted = "TASK_DELETED";
    public const string ReviewRoundReplaced = "REVIEW_ROUND_REPLACED";

    /// <summary>FIX2 K2 — an orphan with no open approval task: there is nothing for the engine's cancel to close. It is
    /// counted apart, not as a failure: one with no task at all is the abandoned-start recovery's (its taskless close and age
    /// floor); one whose tasks are all decided but which is still live is listed for a person to look at (runbook).</summary>
    public const string NoOpenTask = "NO_OPEN_TASK";
    public const string Closed = "CLOSED";

    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration,
        AuditOperation.Execute,
        "WorkflowInstance",
        Guid.Empty,
        SourceModule: "MOD-0023",
        Metadata: new Dictionary<string, object?>
        {
            ["EventName"] = Apply ? "workflow.orphans.close" : "workflow.orphans.dry-run",
            ["Apply"] = Apply,
            ["MaxItems"] = MaxItems,
            // FIX1 (CT decision D) — the one-shot command that ran it, and on which machine. No personal data.
            ["Tool"] = Tool,
            ["Host"] = Host
        });
}

/// <param name="More">The call stopped at <c>MaxItems</c> orphans: call again with <paramref name="NextAfter"/>.</param>
public sealed record WorkflowOrphanSweepResult(
    bool DryRun,
    int Found,
    int Closed,
    IReadOnlyList<WorkflowOrphanItem> Items,
    bool More = false,
    Guid? NextAfter = null);

/// <param name="Outcome">null on a dry run; otherwise <c>CLOSED</c>, <c>NO_OPEN_TASK</c> (nothing to cancel; counted apart),
/// or the reason code the cancel answered.</param>
public sealed record WorkflowOrphanItem(
    Guid WorkflowInstanceId,
    string ObjectType,
    string ObjectId,
    string Status,
    int AgeMinutes,
    string Reason,
    IReadOnlyList<Guid> OpenApprovalTaskIds,
    string? Outcome);
