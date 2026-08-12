using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks;

/// <summary>
/// MOD-0024 — entity → DTO mapping. Enum values are serialized as strings (live Platform convention), and the
/// normalized status / remaining hours are taken from <see cref="ITaskLifecycleService"/> rather than recomputed,
/// so the API and the Task Center projection can never disagree.
/// </summary>
public static class TaskItemMapper
{
    public static TaskItemListItemDto ToListItem(
        TaskItem task,
        ITaskLifecycleService lifecycle,
        bool approvalOutstanding,
        bool approvalRejected = false,
        bool reviewOutstanding = false,
        bool reviewRejected = false) => new(
        task.Id,
        task.Title,
        task.Lifecycle.ToString(),
        lifecycle.ToNormalizedStatus(task, approvalOutstanding, approvalRejected, reviewOutstanding, reviewRejected),
        task.Priority.ToString(),
        task.AssignmentTarget.ToString(),
        task.AssigneeUserId,
        task.PoolPositionId,
        task.OrganizationUnitId,
        task.DueAt,
        task.ReviewRequired,
        task.ApprovalRequired,
        task.Version,
        task.CreatedAt);

    public static TaskItemDetailDto ToDetail(
        TaskItem task,
        ITaskLifecycleService lifecycle,
        bool approvalOutstanding,
        bool approvalRejected,
        IReadOnlyList<TaskWatcher> watchers,
        IReadOnlyList<TaskDependency> dependencies,
        bool reviewOutstanding = false,
        bool reviewRejected = false) => new(
        task.Id,
        task.Title,
        task.Description,
        task.Lifecycle.ToString(),
        lifecycle.ToNormalizedStatus(task, approvalOutstanding, approvalRejected, reviewOutstanding, reviewRejected),
        task.Priority.ToString(),
        task.AssignmentTarget.ToString(),
        task.AssigneeUserId,
        task.PoolPositionId,
        task.CreatedByUserId,
        task.OrganizationUnitId,
        task.DueAt,
        task.StartAt,
        task.PlannedDate,
        task.EstimateHours,
        task.SpentHours,
        lifecycle.CalculateRemainingHours(task),
        task.Tags,
        task.ReviewRequired,
        task.ApprovalRequired,
        task.ApprovalManagerUserId,
        task.WorkflowInstanceId,
        task.EmailNotificationsEnabled,
        // BL-065 — null travels as null: the form shows "everything" for a task whose owner never chose.
        task.NotifyOnEvents,
        task.ReminderLeadDays,
        task.DelegationAllowed,
        task.ProcessInstanceId,
        // A redacted value is OMITTED from the payload — never sent and hidden with CSS (BL-024-ready).
        task.FieldValues
            .Select(v => new TaskFieldValueDto(v.DefinitionCode, v.ValueType, v.Redacted ? null : v.Value))
            .ToList(),
        watchers.Select(w => new TaskWatcherDto(w.Id, w.UserId, w.Role.ToString(), w.PositionId)).ToList(),
        dependencies
            .Select(d => new TaskDependencyDto(d.Id, d.DependsOnTaskItemId, d.DependencyType.ToString()))
            .ToList(),
        task.CompletedAt,
        task.CancelledAt,
        task.ClosureReasonCode,
        task.Version,
        task.CreatedAt,
        task.UpdatedAt,
        task.ReviewerCandidateUserId,
        task.ReviewWorkflowInstanceId,
        // BL-023 — the upward request's instance, so the detail surface can show that the work was ASKED for
        // rather than assigned. A link only; the decision stays MOD-0023's.
        task.RequestWorkflowInstanceId);
}
