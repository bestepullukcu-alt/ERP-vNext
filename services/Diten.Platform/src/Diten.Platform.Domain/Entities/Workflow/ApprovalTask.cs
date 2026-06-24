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
    public DateTimeOffset? TimedOutAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ActionedBy { get; set; }
    public string? ActionReasonCode { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
