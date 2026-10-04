using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Domain.Entities.Workflow;

// MOD-0023 — a unit of human approval action inside a workflow instance. Assignee authority is owned
// by RBAC (MOD-0018); ApprovalTask only references the resolved assignee. Approve/reject/delegate
// transition logic + idempotency enforcement are owned by later batches; Batch 01 scaffolds the entity.
public sealed class ApprovalTask : TenantScopedEntity
{
    public required Guid WorkflowInstanceId { get; set; }
    public string StageCode { get; set; } = "stage-1";
    public string StepCode { get; set; } = "step-1";
    public ApprovalTaskStatus Status { get; set; } = ApprovalTaskStatus.WaitingApproval;
    public Guid? AssignmentSnapshotId { get; set; }

    // Opaque reference to the currently resolved assignee (RBAC/MOD-0018 owns the authority).
    public string? AssigneeRef { get; set; }

    // Populated by the transition batches; reserved here so the storage shape is stable.
    public string? ReasonCode { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public int EscalationLevel { get; set; }
    public string? LastEscalationReasonCode { get; set; }

    /// <summary>
    /// WP-WF-ENGINE-CANDIDATES-01 D2-FIX2 (item 4) — when the SLA sweep looks at this overdue task AGAIN. Set on every task a
    /// sweep could not act on (no SLA rule, nobody eligible, candidates missing, already escalated with no timeout, an
    /// instance that is not active, not yet at its threshold …): the task rests until then, and the sweep's read takes
    /// tasks whose look time has come — never-looked ones first, then by this time — so a standing backlog it cannot act on
    /// no longer fills every page. Null = never looked at. Cleared by an escalation or a timeout.
    ///
    /// <para>It replaces D2-FIX1's <c>EscalationParkedAtUtc</c>, which was written and never read (that field lived only
    /// on this unaccepted branch, so nothing stored carries it). The "warn once" mark stays where it was:
    /// <see cref="LastEscalationReasonCode"/>. A UTC <see cref="DateTime"/>, deliberately: a BSON date (a scalar), not the
    /// [ticks, offset] array a DateTimeOffset becomes (BL-030) — so it can lead an index and a sort, and it adds nothing to
    /// the reviewed DateTimeOffset inventory.</para>
    /// </summary>
    public DateTime? EscalationNextLookAtUtc { get; set; }
    public DateTimeOffset? TimedOutAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ActionedBy { get; set; }
    public string? ActionReasonCode { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
