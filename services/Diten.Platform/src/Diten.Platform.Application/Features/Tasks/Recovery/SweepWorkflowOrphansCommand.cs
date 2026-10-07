using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Application.Features.Workflow;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Recovery;

/// <summary>
/// BL-540 (1) — THE ORPHAN RECOVERY, for an operator holding the non-human key <see cref="WorkflowPermissions.OrphansRecover"/>.
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
public sealed record SweepWorkflowOrphansCommand(
    bool Apply,
    int MaxItems,
    string CorrelationId) : IRequest<Response<WorkflowOrphanSweepResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public const string SystemActor = "system:workflow-orphan-recovery";
    public const string TaskDeleted = "TASK_DELETED";
    public const string ReviewRoundReplaced = "REVIEW_ROUND_REPLACED";

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
            ["MaxItems"] = MaxItems
        });
}

public sealed record WorkflowOrphanSweepRequest(bool Apply = false, int? MaxItems = null);

public sealed record WorkflowOrphanSweepResult(
    bool DryRun,
    int Found,
    int Closed,
    IReadOnlyList<WorkflowOrphanItem> Items);

/// <param name="Outcome">null on a dry run; otherwise <c>CLOSED</c>, or the reason code the cancel answered.</param>
public sealed record WorkflowOrphanItem(
    Guid WorkflowInstanceId,
    string ObjectType,
    string ObjectId,
    string Status,
    int AgeMinutes,
    string Reason,
    IReadOnlyList<Guid> OpenApprovalTaskIds,
    string? Outcome);
