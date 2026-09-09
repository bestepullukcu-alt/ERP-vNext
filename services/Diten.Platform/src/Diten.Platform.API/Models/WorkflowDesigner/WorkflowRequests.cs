using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.WorkflowDesigner;

public sealed class WorkflowStepRequest
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("approver_role")] public string? ApproverRole { get; init; }
    [JsonPropertyName("requires_evidence")] public bool RequiresEvidence { get; init; }
}

public sealed class SlaRuleRequest
{
    [JsonPropertyName("step_sequence")] public int StepSequence { get; init; }
    [JsonPropertyName("sla_hours")] public int SlaHours { get; init; }
    [JsonPropertyName("escalation_action")] public string? EscalationAction { get; init; }
    [JsonPropertyName("escalate_to_role")] public string? EscalateToRole { get; init; }
}

public sealed class CreateWorkflowDefinitionRequest
{
    [JsonPropertyName("code")] public required string Code { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("steps")] public List<WorkflowStepRequest> Steps { get; init; } = [];
    [JsonPropertyName("sla_rules")] public List<SlaRuleRequest> SlaRules { get; init; } = [];
}

public sealed class UpdateWorkflowDefinitionRequest
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("steps")] public List<WorkflowStepRequest> Steps { get; init; } = [];
    [JsonPropertyName("sla_rules")] public List<SlaRuleRequest> SlaRules { get; init; } = [];
    [JsonPropertyName("expected_row_version")] public long ExpectedRowVersion { get; init; }
}

public sealed class PublishWorkflowDefinitionRequest
{
    [JsonPropertyName("expected_row_version")] public long ExpectedRowVersion { get; init; }
}

public sealed class StartWorkflowInstanceRequest
{
    [JsonPropertyName("definition_code")] public required string DefinitionCode { get; init; }
    [JsonPropertyName("subject_reference")] public required string SubjectReference { get; init; }
}

public sealed class ApproveApprovalTaskRequest
{
    [JsonPropertyName("evidence_reference")] public string? EvidenceReference { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
}

public sealed class RejectApprovalTaskRequest
{
    [JsonPropertyName("note")] public string? Note { get; init; }
}

public sealed class DelegateApprovalTaskRequest
{
    [JsonPropertyName("to_assignee_id")] public required string ToAssigneeId { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
}
