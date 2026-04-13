using Diten.Domain.Common;

namespace Diten.Domain.Aggregates.Decomposition;

public sealed class DecompositionStructureAggregate : BaseEntity
{
    public string ParentEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = "New decomposition structure";
    public string StructureType { get; set; } = "PPM_WBS";
    public string Status { get; set; } = "Draft";
    public int Version { get; set; } = 1;
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    public List<DecompositionNode> Nodes { get; set; } = new();
    public List<DecompositionDependency> Dependencies { get; set; } = new();
    public List<DecompositionValidationIssue> ValidationIssues { get; set; } = new();
    public List<DecompositionAuditEvent> AuditTrail { get; set; } = new();
}

public sealed class DecompositionNode : BaseEntity
{
    public string StructureId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "Task";
    public string Description { get; set; } = string.Empty;
    public string ResponsibleName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal? Budget { get; set; }
    public string BudgetMode { get; set; } = "Estimate";
    public decimal ChildRollupBudget { get; set; }
    public int SortOrder { get; set; }
    public int Level { get; set; }
    public string ValidationState { get; set; } = "Unknown";
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class DecompositionDependency : BaseEntity
{
    public string StructureId { get; set; } = string.Empty;
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string DependencyType { get; set; } = "FS";
}

public sealed class DecompositionValidationIssue : BaseEntity
{
    public string StructureId { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Blocking { get; set; }
}

public sealed class DecompositionAuditEvent : BaseEntity
{
    public string StructureId { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventPayload { get; set; } = string.Empty;
    public string Actor { get; set; } = "system";
}
