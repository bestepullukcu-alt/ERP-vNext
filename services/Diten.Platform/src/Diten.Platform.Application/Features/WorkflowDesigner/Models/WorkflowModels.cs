using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.WorkflowDesigner.Models;

public sealed record WorkflowStepModel(Guid StepId, string Name, int Sequence, string? ApproverRole, bool RequiresEvidence);

public sealed record SlaRuleModel(Guid RuleId, int StepSequence, int SlaHours, string EscalationAction, string? EscalateToRole);

public sealed record WorkflowDefinitionListItemModel(
    Guid Id, string Code, string Name, int VersionNumber, string Status, int StepCount, DateTimeOffset CreatedAt);

public sealed record WorkflowDefinitionListModel(
    IReadOnlyList<WorkflowDefinitionListItemModel> Items, int Page, int PageSize, long TotalCount, int TotalPages);

public sealed record WorkflowDefinitionDetailModel(
    Guid Id, string Code, string Name, string? Description, int VersionNumber, string Status,
    IReadOnlyList<WorkflowStepModel> Steps, IReadOnlyList<SlaRuleModel> SlaRules,
    DateTimeOffset? PublishedAt, string? PublishedBy, long RowVersion, DateTimeOffset CreatedAt);

public sealed record WorkflowTimelineEntryModel(int Sequence, string Kind, int StepSequence, string Actor, DateTimeOffset At, string? Note);

public sealed record WorkflowInstanceStepModel(
    Guid StepId, string Name, int Sequence, string? ApproverRole, bool RequiresEvidence, int? SlaHours, string EscalationAction);

public sealed record WorkflowInstanceListItemModel(
    Guid Id, Guid DefinitionId, string DefinitionCode, int DefinitionVersion, string SubjectReference,
    string Status, int CurrentStepSequence, DateTimeOffset StartedAt);

public sealed record WorkflowInstanceListModel(
    IReadOnlyList<WorkflowInstanceListItemModel> Items, int Page, int PageSize, long TotalCount, int TotalPages);

public sealed record WorkflowInstanceDetailModel(
    Guid Id, Guid DefinitionId, string DefinitionCode, int DefinitionVersion, string SubjectReference,
    string Status, int CurrentStepSequence, IReadOnlyList<WorkflowInstanceStepModel> Steps,
    IReadOnlyList<WorkflowTimelineEntryModel> Timeline, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, long RowVersion);

public sealed record ApprovalTaskListItemModel(
    Guid Id, Guid WorkflowInstanceId, string DefinitionCode, int StepSequence, string StepName,
    string? AssigneeRole, string? AssigneeId, string Status, bool RequiresEvidence, DateTimeOffset? DueAt, DateTimeOffset CreatedAt);

public sealed record ApprovalTaskListModel(
    IReadOnlyList<ApprovalTaskListItemModel> Items, int Page, int PageSize, long TotalCount, int TotalPages);

public sealed record ApprovalTaskDetailModel(
    Guid Id, Guid WorkflowInstanceId, string DefinitionCode, Guid StepId, int StepSequence, string StepName,
    string? AssigneeRole, string? AssigneeId, string Status, bool RequiresEvidence, string? EvidenceReference,
    string? Decision, string? DecisionBy, DateTimeOffset? DecisionAt, string? Note, DateTimeOffset? DueAt,
    string EscalationAction, long RowVersion);

public static class WorkflowMappings
{
    public static WorkflowStepModel ToModel(WorkflowStep s) => new(s.StepId, s.Name, s.Sequence, s.ApproverRole, s.RequiresEvidence);
    public static SlaRuleModel ToModel(SlaEscalationRule r) => new(r.RuleId, r.StepSequence, r.SlaHours, r.EscalationAction.ToString(), r.EscalateToRole);

    public static WorkflowDefinitionListItemModel ToListItem(WorkflowDefinition e) =>
        new(e.WorkflowDefinitionId, e.Code, e.Name, e.VersionNumber, e.Status.ToString(), e.Steps.Count, e.CreatedAt);

    public static WorkflowDefinitionDetailModel ToDetail(WorkflowDefinition e) => new(
        e.WorkflowDefinitionId, e.Code, e.Name, e.Description, e.VersionNumber, e.Status.ToString(),
        e.Steps.OrderBy(s => s.Sequence).Select(ToModel).ToList(),
        e.SlaRules.OrderBy(r => r.StepSequence).Select(ToModel).ToList(),
        e.PublishedAt, e.PublishedBy, e.RowVersion, e.CreatedAt);

    public static WorkflowInstanceStepModel ToModel(WorkflowInstanceStep s) =>
        new(s.StepId, s.Name, s.Sequence, s.ApproverRole, s.RequiresEvidence, s.SlaHours, s.EscalationAction.ToString());

    public static WorkflowTimelineEntryModel ToModel(WorkflowTimelineEntry t) =>
        new(t.Sequence, t.Kind, t.StepSequence, t.Actor, t.At, t.Note);

    public static WorkflowInstanceListItemModel ToListItem(WorkflowInstance e) => new(
        e.WorkflowInstanceId, e.DefinitionId, e.DefinitionCode, e.DefinitionVersion, e.SubjectReference,
        e.Status.ToString(), e.CurrentStepSequence, e.StartedAt);

    public static WorkflowInstanceDetailModel ToDetail(WorkflowInstance e) => new(
        e.WorkflowInstanceId, e.DefinitionId, e.DefinitionCode, e.DefinitionVersion, e.SubjectReference,
        e.Status.ToString(), e.CurrentStepSequence,
        e.Steps.OrderBy(s => s.Sequence).Select(ToModel).ToList(),
        e.Timeline.OrderBy(t => t.Sequence).Select(ToModel).ToList(),
        e.StartedAt, e.CompletedAt, e.RowVersion);

    public static ApprovalTaskListItemModel ToListItem(ApprovalTask e) => new(
        e.ApprovalTaskId, e.WorkflowInstanceId, e.DefinitionCode, e.StepSequence, e.StepName,
        e.AssigneeRole, e.AssigneeId, e.Status.ToString(), e.RequiresEvidence, e.DueAt, e.CreatedAt);

    public static ApprovalTaskDetailModel ToDetail(ApprovalTask e) => new(
        e.ApprovalTaskId, e.WorkflowInstanceId, e.DefinitionCode, e.StepId, e.StepSequence, e.StepName,
        e.AssigneeRole, e.AssigneeId, e.Status.ToString(), e.RequiresEvidence, e.EvidenceReference,
        e.Decision, e.DecisionBy, e.DecisionAt, e.Note, e.DueAt, e.EscalationAction.ToString(), e.RowVersion);
}
