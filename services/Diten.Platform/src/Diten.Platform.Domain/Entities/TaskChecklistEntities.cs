using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// MOD-0024 Task &amp; Checklist Engine — generic operational Task primitive (SoR).
/// Distinct from approval semantics (MOD-0023). Tenant-scoped, soft-deleted, optimistic-concurrency.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class WorkTask : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid WorkTaskId { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Open;

    public string? AssigneeId { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public TaskEscalationPolicy EscalationPolicy { get; set; } = TaskEscalationPolicy.None;

    [BsonRepresentation(BsonType.String)]
    public Guid? ChecklistRunId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? TemplateItemId { get; set; }

    public bool RequiresEvidence { get; set; }
    public string? EvidenceReference { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    public long RowVersion { get; set; } = 1;
}

/// <summary>
/// MOD-0024 owned object: an assignment record for a <see cref="WorkTask"/>.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class TaskAssignment : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid TaskAssignmentId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid WorkTaskId { get; set; }

    public string AssigneeId { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    public long RowVersion { get; set; } = 1;
}

/// <summary>
/// MOD-0024 owned object: a reusable checklist template (catalog).
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ChecklistTemplate : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ChecklistTemplateId { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ChecklistTemplateStatus Status { get; set; } = ChecklistTemplateStatus.Active;

    public List<ChecklistTemplateItem> Items { get; set; } = [];

    public long RowVersion { get; set; } = 1;
}

[BsonIgnoreExtraElements]
public sealed class ChecklistTemplateItem
{
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public bool RequiresEvidence { get; set; }
    public int Sequence { get; set; }
}

/// <summary>
/// MOD-0024 owned object: an in-flight or completed execution of a <see cref="ChecklistTemplate"/>.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ChecklistRun : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ChecklistRunId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid TemplateId { get; set; }

    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ChecklistRunStatus Status { get; set; } = ChecklistRunStatus.InProgress;

    public List<ChecklistRunItem> Items { get; set; } = [];

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public long RowVersion { get; set; } = 1;
}

[BsonIgnoreExtraElements]
public sealed class ChecklistRunItem
{
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public bool RequiresEvidence { get; set; }
    public int Sequence { get; set; }

    public bool IsCompleted { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
}
