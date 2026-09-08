using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.TaskChecklistEngine.Models;

public sealed record WorkTaskListItemModel(
    Guid Id,
    string Title,
    string Status,
    string? AssigneeId,
    DateTimeOffset? DueDate,
    string EscalationPolicy,
    bool RequiresEvidence,
    DateTimeOffset CreatedAt);

public sealed record WorkTaskListModel(
    IReadOnlyList<WorkTaskListItemModel> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record WorkTaskDetailModel(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string? AssigneeId,
    DateTimeOffset? DueDate,
    string EscalationPolicy,
    Guid? ChecklistRunId,
    Guid? TemplateItemId,
    bool RequiresEvidence,
    string? EvidenceReference,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    long RowVersion,
    DateTimeOffset CreatedAt);

public sealed record ChecklistTemplateItemModel(
    Guid ItemId,
    string Title,
    bool RequiresEvidence,
    int Sequence);

public sealed record ChecklistTemplateListItemModel(
    Guid Id,
    string Code,
    string Name,
    string Status,
    int ItemCount,
    DateTimeOffset CreatedAt);

public sealed record ChecklistTemplateListModel(
    IReadOnlyList<ChecklistTemplateListItemModel> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record ChecklistTemplateDetailModel(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Status,
    IReadOnlyList<ChecklistTemplateItemModel> Items,
    long RowVersion,
    DateTimeOffset CreatedAt);

public sealed record ChecklistRunItemModel(
    Guid ItemId,
    string Title,
    bool RequiresEvidence,
    int Sequence,
    bool IsCompleted,
    string? EvidenceReference,
    DateTimeOffset? CompletedAt,
    string? CompletedBy);

public sealed record ChecklistRunListItemModel(
    Guid Id,
    Guid TemplateId,
    string TemplateCode,
    string Name,
    string Status,
    int TotalItems,
    int CompletedItems,
    DateTimeOffset StartedAt);

public sealed record ChecklistRunListModel(
    IReadOnlyList<ChecklistRunListItemModel> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record ChecklistRunDetailModel(
    Guid Id,
    Guid TemplateId,
    string TemplateCode,
    string Name,
    string Status,
    IReadOnlyList<ChecklistRunItemModel> Items,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    long RowVersion);

public static class TaskChecklistMappings
{
    public static WorkTaskListItemModel ToListItem(WorkTask e) => new(
        e.WorkTaskId, e.Title, e.Status.ToString(), e.AssigneeId, e.DueDate,
        e.EscalationPolicy.ToString(), e.RequiresEvidence, e.CreatedAt);

    public static WorkTaskDetailModel ToDetail(WorkTask e) => new(
        e.WorkTaskId, e.Title, e.Description, e.Status.ToString(), e.AssigneeId, e.DueDate,
        e.EscalationPolicy.ToString(), e.ChecklistRunId, e.TemplateItemId, e.RequiresEvidence,
        e.EvidenceReference, e.CompletedAt, e.CompletedBy, e.RowVersion, e.CreatedAt);

    public static ChecklistTemplateItemModel ToItem(ChecklistTemplateItem i) => new(
        i.ItemId, i.Title, i.RequiresEvidence, i.Sequence);

    public static ChecklistTemplateListItemModel ToListItem(ChecklistTemplate e) => new(
        e.ChecklistTemplateId, e.Code, e.Name, e.Status.ToString(), e.Items.Count, e.CreatedAt);

    public static ChecklistTemplateDetailModel ToDetail(ChecklistTemplate e) => new(
        e.ChecklistTemplateId, e.Code, e.Name, e.Description, e.Status.ToString(),
        e.Items.OrderBy(i => i.Sequence).Select(ToItem).ToList(), e.RowVersion, e.CreatedAt);

    public static ChecklistRunItemModel ToItem(ChecklistRunItem i) => new(
        i.ItemId, i.Title, i.RequiresEvidence, i.Sequence, i.IsCompleted,
        i.EvidenceReference, i.CompletedAt, i.CompletedBy);

    public static ChecklistRunListItemModel ToListItem(ChecklistRun e) => new(
        e.ChecklistRunId, e.TemplateId, e.TemplateCode, e.Name, e.Status.ToString(),
        e.Items.Count, e.Items.Count(i => i.IsCompleted), e.StartedAt);

    public static ChecklistRunDetailModel ToDetail(ChecklistRun e) => new(
        e.ChecklistRunId, e.TemplateId, e.TemplateCode, e.Name, e.Status.ToString(),
        e.Items.OrderBy(i => i.Sequence).Select(ToItem).ToList(), e.StartedAt, e.CompletedAt, e.RowVersion);
}
