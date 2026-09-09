using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// MOD-0023 Workflow Designer — a versioned, explicitly-published approval workflow definition (SoR).
/// Approvals-focused (no BPMN). Tenant-scoped, soft-deleted, optimistic-concurrency.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class WorkflowDefinition : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid WorkflowDefinitionId { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int VersionNumber { get; set; } = 1;
    public WorkflowDefinitionStatus Status { get; set; } = WorkflowDefinitionStatus.Draft;

    public List<WorkflowStep> Steps { get; set; } = [];
    public List<SlaEscalationRule> SlaRules { get; set; } = [];

    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }

    public long RowVersion { get; set; } = 1;
}

[BsonIgnoreExtraElements]
public sealed class WorkflowStep
{
    [BsonRepresentation(BsonType.String)]
    public Guid StepId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string? ApproverRole { get; set; }
    public bool RequiresEvidence { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class SlaEscalationRule
{
    [BsonRepresentation(BsonType.String)]
    public Guid RuleId { get; set; } = Guid.NewGuid();

    public int StepSequence { get; set; }
    public int SlaHours { get; set; }
    public WorkflowEscalationAction EscalationAction { get; set; } = WorkflowEscalationAction.None;
    public string? EscalateToRole { get; set; }
}

/// <summary>
/// MOD-0023 owned object: a running instance of a WorkflowDefinition, version-pinned via a snapshot
/// of the definition steps so runtime advancement never depends on a later definition edit.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class WorkflowInstance : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid WorkflowInstanceId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid DefinitionId { get; set; }

    public string DefinitionCode { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; }
    public string SubjectReference { get; set; } = string.Empty;

    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;
    public int CurrentStepSequence { get; set; }

    public List<WorkflowInstanceStep> Steps { get; set; } = [];
    public List<WorkflowTimelineEntry> Timeline { get; set; } = [];

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public long RowVersion { get; set; } = 1;
}

[BsonIgnoreExtraElements]
public sealed class WorkflowInstanceStep
{
    [BsonRepresentation(BsonType.String)]
    public Guid StepId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string? ApproverRole { get; set; }
    public bool RequiresEvidence { get; set; }
    public int? SlaHours { get; set; }
    public WorkflowEscalationAction EscalationAction { get; set; } = WorkflowEscalationAction.None;
    public string? EscalateToRole { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class WorkflowTimelineEntry
{
    [BsonRepresentation(BsonType.String)]
    public Guid EntryId { get; set; } = Guid.NewGuid();

    public int Sequence { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int StepSequence { get; set; }
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
}

/// <summary>
/// MOD-0023 owned object: an approval decision task for one step of a running instance.
/// This is an APPROVAL task (approve/reject/delegate) — distinct from MOD-0024 operational tasks,
/// which remain owned by the Task &amp; Checklist Engine and are not duplicated here.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ApprovalTask : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ApprovalTaskId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid WorkflowInstanceId { get; set; }

    public string DefinitionCode { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid StepId { get; set; }

    public int StepSequence { get; set; }
    public string StepName { get; set; } = string.Empty;

    public string? AssigneeRole { get; set; }
    public string? AssigneeId { get; set; }

    public ApprovalTaskStatus Status { get; set; } = ApprovalTaskStatus.Pending;

    public bool RequiresEvidence { get; set; }
    public string? EvidenceReference { get; set; }

    public string? Decision { get; set; }
    public string? DecisionBy { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset? DueAt { get; set; }
    public WorkflowEscalationAction EscalationAction { get; set; } = WorkflowEscalationAction.None;

    public long RowVersion { get; set; } = 1;
}
