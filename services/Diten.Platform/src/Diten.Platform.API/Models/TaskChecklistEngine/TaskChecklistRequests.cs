using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.TaskChecklistEngine;

public sealed class CreateWorkTaskRequest
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("assignee_id")]
    public string? AssigneeId { get; init; }

    [JsonPropertyName("due_date")]
    public DateTimeOffset? DueDate { get; init; }

    [JsonPropertyName("escalation_policy")]
    public string? EscalationPolicy { get; init; }

    [JsonPropertyName("requires_evidence")]
    public bool RequiresEvidence { get; init; }
}

public sealed class AssignWorkTaskRequest
{
    [JsonPropertyName("assignee_id")]
    public required string AssigneeId { get; init; }
}

public sealed class CompleteWorkTaskRequest
{
    [JsonPropertyName("evidence_reference")]
    public string? EvidenceReference { get; init; }
}

public sealed class ChecklistTemplateItemRequest
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("requires_evidence")]
    public bool RequiresEvidence { get; init; }
}

public sealed class CreateChecklistTemplateRequest
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("items")]
    public List<ChecklistTemplateItemRequest> Items { get; init; } = [];
}

public sealed class UpdateChecklistTemplateRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("items")]
    public List<ChecklistTemplateItemRequest> Items { get; init; } = [];

    [JsonPropertyName("expected_row_version")]
    public long ExpectedRowVersion { get; init; }
}

public sealed class StartChecklistRunRequest
{
    [JsonPropertyName("template_id")]
    public required Guid TemplateId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed class CompleteChecklistRunItemRequest
{
    [JsonPropertyName("evidence_reference")]
    public string? EvidenceReference { get; init; }
}
