using Diten.Platform.Application.Contracts;
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
        /// <summary>
        /// BL-024 Phase 2 — who is asking, and the catalogue their question is answered against.
        ///
        /// <para>REQUIRED, with no default. A default would have to be "permit everything", and a fail-open
        /// default on a security decision is the one mistake that is invisible in review: every existing caller
        /// would keep compiling and keep leaking. Making it required means the compiler names every read path
        /// that has to answer the question.</para>
        /// </summary>
        IActorPermissionContext actor,
        IReadOnlyDictionary<string, TaskFieldDefinition>? definitions,
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
        /*
         * BL-024 Phase 2 — the value a caller may not see NEVER LEAVES THE SERVER.
         *
         * `Redacted` was inert before this: the mapper honoured the flag and nothing ever set it, so the
         * mechanism worked and no field was ever hidden. The decision now comes from TaskFieldAccessRules, one
         * place, consulted here and by the Work Center projection and the write validator and the options
         * endpoint — four call sites, one rule.
         *
         * The value is replaced with null rather than the row being dropped: the field's PRESENCE is not a
         * secret (the catalogue is readable), only its content is, and dropping the row would make a hidden
         * field indistinguishable from a field that does not exist. `redacted: true` says which.
         */
        task.FieldValues
            .Select(v =>
            {
                var visible = TaskFieldAccessRules.CanView(v, definitions?.GetValueOrDefault(v.DefinitionCode), actor);
                return new TaskFieldValueDto(v.DefinitionCode, v.ValueType, visible ? v.Value : null, !visible);
            })
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
        task.RequestWorkflowInstanceId,
        /*
         * DCP-005 slice 3 — the frozen six, copied out as they were stored. Nothing here consults the register:
         * a read path that re-resolved a citation would undo the freezing on every page load, and would look
         * exactly like this line while doing it.
         */
        task.DocumentReferences
            .Select(r => new TaskDocumentReferenceDto(
                r.DocumentUid, r.DocumentCode, r.Title, r.DocumentVersion, r.Status, r.ReferencedAt,
                r.ListVersionId))
            .ToList(),
        task.TaskTypeId);
}
