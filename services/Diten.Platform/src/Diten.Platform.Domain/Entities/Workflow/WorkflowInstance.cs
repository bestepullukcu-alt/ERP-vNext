using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Domain.Entities.Workflow;

// MOD-0023 — a running workflow over a source business object. The instance carries only an opaque
// reference to the source object (module / type / id); it never stores business-object lifecycle state
// (that remains owned by the source business module). Instance start + version pinning are owned by a
// later batch; Batch 01 only scaffolds the entity and its tenant-scoped persistence.
public sealed class WorkflowInstance : TenantScopedEntity
{
    public required Guid TemplateId { get; set; }
    public required Guid WorkflowTemplateId { get; set; }

    // Version pinning is performed by the instance-start batch; null until then.
    public Guid? TemplateVersionId { get; set; }

    // Opaque reference to the source business object — no business state is copied here.
    public required string ObjectType { get; set; }
    public required string ObjectId { get; set; }

    // Combined reference (e.g. "{module}|{objectType}|{objectId}") used by the transition gate lookup.
    public required string ObjectRef { get; set; }

    public string CurrentStage { get; set; } = "stage-1";
    public string CurrentStep { get; set; } = "step-1";
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Pending;

    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? StartedBy { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LastTransitionAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
