using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Domain.Entities.Workflow;

// MOD-0023 — append-only record of a workflow instance/task transition. This is the workflow engine's
// own transition history (distinct from the MOD-0021 audit trail). The engine that emits these rows is
// owned by later batches; Batch 01 scaffolds the entity and its tenant-scoped persistence.
public sealed class WorkflowTransitionLog : TenantScopedEntity
{
    public required Guid WorkflowInstanceId { get; set; }
    public Guid? ApprovalTaskId { get; set; }
    public WorkflowTransitionAction Action { get; set; }

    public string? FromState { get; set; }
    public string? ToState { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? ActorId { get; set; }
    public string? ActorRef { get; set; }
    public string? ReasonCode { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? Comment { get; set; }
    public string? EvidenceRef { get; set; }
    public string? CorrelationId { get; set; }

    // Monotonic per-instance sequence; the transition batch assigns it. Append-only (no update/delete).
    public long SequenceNo { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
