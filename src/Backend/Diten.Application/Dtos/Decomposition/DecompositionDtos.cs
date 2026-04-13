namespace Diten.Application.Dtos.Decomposition;

public sealed class DecompositionStructureDto
{
    public string Id { get; set; } = string.Empty;
    public string ParentEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StructureType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public IReadOnlyList<DecompositionNodeDto> Nodes { get; set; } = Array.Empty<DecompositionNodeDto>();
    public IReadOnlyList<DecompositionDependencyDto> Dependencies { get; set; } = Array.Empty<DecompositionDependencyDto>();
    public DecompositionValidationSummaryDto ValidationSummary { get; set; } = new();
}

public sealed class DecompositionNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResponsibleName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Budget { get; set; }
    public string BudgetMode { get; set; } = string.Empty;
    public decimal ChildRollupBudget { get; set; }
    public int SortOrder { get; set; }
    public int Level { get; set; }
    public string ValidationState { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class DecompositionDependencyDto
{
    public string Id { get; set; } = string.Empty;
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string DependencyType { get; set; } = string.Empty;
}

public sealed class DecompositionValidationIssueDto
{
    public string Id { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Blocking { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class DecompositionAuditEventDto
{
    public string Id { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventPayload { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class DecompositionValidationSummaryDto
{
    public int TotalIssues { get; set; }
    public int BlockingIssues { get; set; }
    public int WarningIssues { get; set; }
    public int ReadinessPercent { get; set; }
}

public sealed class CreateStructureRequest
{
    public string ParentEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = "New decomposition structure";
    public string StructureType { get; set; } = "PPM_WBS";
}

public sealed class UpdateStructureRequest
{
    public int ExpectedVersion { get; set; }
    public string? Name { get; set; }
    public string? StructureType { get; set; }
}

public sealed class CreateNodeRequest
{
    public int ExpectedVersion { get; set; }
    public string? ParentId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
}

public sealed class UpdateNodeRequest
{
    public int ExpectedVersion { get; set; }
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? ResponsibleName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public decimal? Budget { get; set; }
    public string? BudgetMode { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class AddSiblingRequest
{
    public int ExpectedVersion { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
}

public sealed class MoveNodeRequest
{
    public int ExpectedVersion { get; set; }
    public string? TargetParentId { get; set; }
    public int TargetIndex { get; set; } = -1;
    public string PlacementMode { get; set; } = "child";
}

public sealed class ReorderNodeRequest
{
    public int ExpectedVersion { get; set; }
    public int TargetIndex { get; set; }
}

public sealed class ApproveStructureRequest
{
    public int ExpectedVersion { get; set; }
}

public sealed class AddDependencyRequest
{
    public int ExpectedVersion { get; set; }
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string DependencyType { get; set; } = "FS";
}

public sealed class DeleteDependencyRequest
{
    public int ExpectedVersion { get; set; }
}

public sealed class ActionResultEnvelope<T>
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}
