using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// MOD-0024 — task creation. The three assignment targets (self / person / position pool) all land here; the
/// contract projection triple is derived later by <see cref="ITaskAssignmentResolver"/>, so this handler only has
/// to persist a coherent intent.
///
/// <para>Deliberate omissions: the caller cannot set the lifecycle (the system does — pack §12 Y2) and cannot set
/// SpentHours (always 0 on a new task — pack §12 Y1). Approval/review are recorded as FLAGS only; MOD-0023 owns
/// the decision and its handoff is Phase 3 (pack §12 K2).</para>
/// </summary>
public sealed class CreateTaskItemHandler : IRequestHandler<CreateTaskItemCommand, Response<Guid>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskWatcherRepository _watchers;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly ITaskFieldDefinitionService _fieldDefinitions;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ITaskApprovalService _approvals;
    private readonly IChecklistTemplateRepository _checklistTemplates;
    private readonly IChecklistRunRepository _checklistRuns;
    private readonly ITaskChecklistService _checklistService;
    private readonly INotificationEventDispatchAdapter _notifications;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateTaskItemHandler> _logger;

    public CreateTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskWatcherRepository watchers,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        IPositionAssignmentRepository positionAssignments,
        ITaskFieldDefinitionService fieldDefinitions,
        ITaskLifecycleService lifecycle,
        ITaskApprovalService approvals,
        IChecklistTemplateRepository checklistTemplates,
        IChecklistRunRepository checklistRuns,
        ITaskChecklistService checklistService,
        INotificationEventDispatchAdapter notifications,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        ILogger<CreateTaskItemHandler> logger)
    {
        _tasks = tasks;
        _assignments = assignments;
        _watchers = watchers;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _positionAssignments = positionAssignments;
        _fieldDefinitions = fieldDefinitions;
        _lifecycle = lifecycle;
        _approvals = approvals;
        _checklistTemplates = checklistTemplates;
        _checklistRuns = checklistRuns;
        _checklistService = checklistService;
        _notifications = notifications;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreateTaskItemCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actorId = _currentUser.UserId;

        // ── Assignment intent (pack §12 K5) ──────────────────────────────────
        Guid? assigneeUserId;
        Guid? poolPositionId = null;
        Guid organizationUnitId;

        /*
         * The SHARED rule. It used to be written out inline here, which was fine while creation was the only
         * thing that assigned work — recurrence rules now carry an assignment too, and a second copy there is
         * how the reviewer defect happened a slice ago. `allowSelfAssigned: true` because this path HAS a
         * caller; the recurring sweep does not, which is the one thing the two callers disagree about.
         */
        if (TaskAssignmentIntentRules.Validate(
                request.AssignmentTarget, request.AssigneeUserId, request.PoolPositionId, allowSelfAssigned: true)
            is { } invalid)
        {
            return Fail(invalid.Message, invalid.ReasonCode, command.CorrelationId);
        }

        switch (request.AssignmentTarget)
        {
            case TaskAssignmentTarget.SelfAssigned:
                assigneeUserId = actorId;
                break;

            case TaskAssignmentTarget.Person:
                assigneeUserId = request.AssigneeUserId;
                break;

            case TaskAssignmentTarget.PositionPool:
                // A pool task has NO holder until someone claims it — that is the point of a pool.
                assigneeUserId = null;
                poolPositionId = request.PoolPositionId;
                break;

            default:
                return Fail("Unsupported assignment target.",
                    TaskReasonCodes.AssignmentTargetInvalid, command.CorrelationId);
        }

        // ── Pool position must be genuinely assignable ───────────────────────
        if (poolPositionId is { } positionId)
        {
            var position = await _positions.GetByIdAsync(positionId, ct);
            if (position is null || position.IsArchived || position.Status != PositionStatus.Active)
            {
                return Fail("The selected position is not assignable.",
                    TaskReasonCodes.PositionNotAssignable, command.CorrelationId);
            }

            // A Position is always unit-bound, so the pool inherits the facility automatically (pack §12 K4/K6).
            organizationUnitId = request.OrganizationUnitId ?? position.OrganizationUnitId;
        }
        else
        {
            // Graded fallback (pack §12 K6 — every task HAS a unit, and the user never picks one):
            //   1. explicit request value
            //   2. the assignee's active position's unit
            //   3. the tenant's root unit — so a person holding no position can still create work
            //   4. otherwise fail, because inventing a unit would silently misfile the task
            var resolved = request.OrganizationUnitId
                           ?? await ResolveUnitForUserAsync(assigneeUserId!.Value, ct)
                           ?? await ResolveTenantRootUnitAsync(ct);
            if (resolved is null || resolved == Guid.Empty)
            {
                return Fail("The organization unit could not be resolved for the assignee.",
                    TaskReasonCodes.OrganizationUnitUnresolved, command.CorrelationId);
            }

            organizationUnitId = resolved.Value;
        }

        // The org unit must exist and be usable (pack §12 K6).
        var unit = await _organizationUnits.GetByIdAsync(organizationUnitId, ct);
        if (unit is null || unit.IsArchived)
        {
            return Fail("The organization unit could not be resolved.",
                TaskReasonCodes.OrganizationUnitUnresolved, command.CorrelationId);
        }

        // ── Configurable fields (pack §12 K1) ───────────────────────────────
        var fields = await _fieldDefinitions.ValidateAndMaterializeAsync(request.FieldValues, ct);
        if (!fields.IsValid)
        {
            return Fail(fields.Message ?? "Invalid task field value.",
                fields.ReasonCode ?? TaskReasonCodes.FieldValueInvalid, command.CorrelationId);
        }

        if (request.ApprovalRequired
            && (request.ApprovalManagerUserId is null || request.ApprovalManagerUserId == Guid.Empty))
        {
            return Fail("An approval manager is required when approval is requested.",
                TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        /*
         * The review's symmetric rule. Its own reason code rather than the generic VALIDATION_FAILED approval
         * uses, because this one names a field the form has to point at — and because "a review was requested
         * with nobody to review it" is a specific mistake the client can explain, not a generic bad payload.
         */
        if (TaskReviewRules.ReviewerMissing(request.ReviewRequired, request.ReviewerCandidateUserId))
        {
            return Fail(TaskReviewRules.ReviewerRequiredMessage,
                TaskReasonCodes.ReviewerRequired, command.CorrelationId);
        }

        // ── Subtask link (pack §12 E2): validated before anything is written ──
        if (request.ParentTaskItemId is { } parentId && parentId != Guid.Empty)
        {
            // The tenant-scoped repository makes this a cross-tenant check too: another tenant's parent simply
            // does not resolve.
            var parent = await _tasks.GetByIdAsync(parentId, ct);
            if (parent is null)
            {
                return Fail("The parent task could not be found.",
                    TaskReasonCodes.ParentTaskNotFound, command.CorrelationId);
            }

            // ONE LEVEL ONLY. Enforced on the server because the rule is the model's, not the form's: deeper
            // hierarchies belong to the source system the Task Center deep-links to.
            if (parent.ParentTaskItemId is not null)
            {
                return Fail("A subtask cannot itself have subtasks.",
                    TaskReasonCodes.SubtaskDepthExceeded, command.CorrelationId);
            }
        }

        var task = new TaskItem
        {
            TenantId = _tenantContext.TenantId,
            ParentTaskItemId = request.ParentTaskItemId is { } p && p != Guid.Empty ? p : null,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            // SYSTEM decides the initial lifecycle; the request has no say (pack §12 Y2).
            Lifecycle = _lifecycle.ResolveInitialLifecycle(request.ApprovalRequired),
            Priority = request.Priority,
            AssignmentTarget = request.AssignmentTarget,
            AssigneeUserId = assigneeUserId,
            PoolPositionId = poolPositionId,
            CreatedByUserId = actorId,
            OrganizationUnitId = organizationUnitId,
            DueAt = request.DueAt,
            StartAt = request.StartAt,
            PlannedDate = request.PlannedDate,
            EstimateHours = request.EstimateHours,
            // Always zero on a new task; the request cannot carry it (pack §12 Y1).
            SpentHours = 0m,
            Tags = (request.Tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList(),
            ReviewRequired = request.ReviewRequired,
            ReviewerCandidateUserId = request.ReviewerCandidateUserId,
            ApprovalRequired = request.ApprovalRequired,
            ApprovalManagerUserId = request.ApprovalManagerUserId,
            EmailNotificationsEnabled = request.EmailNotificationsEnabled,
            DelegationAllowed = request.DelegationAllowed,
            FieldValues = fields.Values.ToList(),
            CreatedBy = _currentUser.ActorName
        };

        await _tasks.CreateAsync(task, ct);

        // ── Approval handoff to MOD-0023 (pack §12 K2) ───────────────────────
        // Started after creation because the instance references the task id. If it cannot start, the TASK IS
        // KEPT with ApprovalRequired still true and no instance id: the fail-closed gate then holds `start` shut
        // until it is retried, so the user's work survives without ever becoming startable un-approved.
        if (task.ApprovalRequired)
        {
            var instanceId = await _approvals.TryStartApprovalAsync(task, ct);
            if (instanceId is not null)
            {
                task.WorkflowInstanceId = instanceId;
                await _tasks.UpdateAsync(task, task.Version, ct);
            }
        }

        // A checklist template becomes a live run on the new task (pack §12 E1/E5). After creation so the run can
        // carry the task's id; a missing or inactive template is logged and skipped rather than failing the
        // creation — losing the task because a template vanished is the worse outcome.
        if (request.ChecklistTemplateId is { } checklistTemplateId && checklistTemplateId != Guid.Empty)
        {
            var checklistTemplate = await _checklistTemplates.GetByIdAsync(checklistTemplateId, ct);
            if (checklistTemplate is not null && checklistTemplate.IsActive)
            {
                await _checklistRuns.CreateAsync(
                    _checklistService.Instantiate(_tenantContext.TenantId, task.Id, checklistTemplate), ct);
            }
            else
            {
                _logger.LogWarning(
                    "Checklist template {TemplateId} requested for task {TaskId} is missing or inactive; "
                    + "the task was created WITHOUT a checklist.",
                    checklistTemplateId, task.Id);
            }
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Created,
            UserId = assigneeUserId,
            PositionId = poolPositionId,
            ActorUserId = actorId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        // Watchers/consultants: visibility only, never action rights (pack §12 K3, OD-4).
        foreach (var watcher in request.Watchers ?? [])
        {
            if (watcher.UserId == Guid.Empty)
            {
                continue;
            }

            await _watchers.CreateAsync(new TaskWatcher
            {
                TenantId = _tenantContext.TenantId,
                TaskItemId = task.Id,
                UserId = watcher.UserId,
                Role = watcher.Role,
                PositionId = watcher.PositionId,
                AddedByUserId = actorId,
                CreatedBy = _currentUser.ActorName
            }, ct);
        }

        await NotifyAssignedAsync(task, ct);

        return Response<Guid>.Success(task.Id, 201, command.CorrelationId);
    }

    /// <summary>
    /// Assignment email. A dispatch failure must NEVER fail task creation: the adapter returns a controlled
    /// Response and never throws, so we log and move on (pack §13).
    /// </summary>
    private async Task NotifyAssignedAsync(TaskItem task, CancellationToken ct)
    {
        if (!task.EmailNotificationsEnabled)
        {
            return;
        }

        // Self-assigned work needs no "you were assigned" email — the actor just created it.
        if (task.AssignmentTarget == TaskAssignmentTarget.SelfAssigned)
        {
            return;
        }

        try
        {
            var recipients = await ResolveRecipientsAsync(task, ct);
            if (recipients.Count == 0)
            {
                return;
            }

            var response = await _notifications.DispatchByEventCodeAsync(new NotificationEventDispatchRequest(
                TenantId: _tenantContext.TenantId,
                EventCode: TaskNotificationEvents.Assigned,
                To: recipients,
                Variables: new Dictionary<string, object?>
                {
                    ["TaskTitle"] = task.Title,
                    ["TaskId"] = task.Id.ToString(),
                    ["DueAt"] = task.DueAt?.ToString("yyyy-MM-dd") ?? string.Empty
                }),
                ct);

            if (!response.IsSuccessful)
            {
                // Most common in a fresh environment: the manifest-declared event is still Draft
                // (EVENT_NOT_ACTIVE). That is an ops condition, not a task failure.
                _logger.LogWarning(
                    "task.assigned notification not dispatched. TaskId={TaskId} ReasonCode={ReasonCode}",
                    task.Id, response.ReasonCode);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "task.assigned notification failed. TaskId={TaskId}", task.Id);
        }
    }

    /// <summary>
    /// Person target → the assignee. Pool target → every active holder of the position (OD-2). After a claim,
    /// the other holders are not notified again.
    /// </summary>
    private async Task<IReadOnlyList<EmailRecipientDto>> ResolveRecipientsAsync(TaskItem task, CancellationToken ct)
    {
        var userIds = new List<Guid>();

        if (task.AssignmentTarget == TaskAssignmentTarget.Person && task.AssigneeUserId is { } assignee)
        {
            userIds.Add(assignee);
        }
        else if (task.AssignmentTarget == TaskAssignmentTarget.PositionPool && task.PoolPositionId is { } positionId)
        {
            var now = DateTimeOffset.UtcNow;
            var assignments = await _positionAssignments.GetAllAsync(ct);
            userIds.AddRange(assignments
                .Where(a => a.PositionId == positionId
                            && !a.IsCancelled
                            && a.EffectiveFrom <= now
                            && (a.EffectiveTo is null || a.EffectiveTo > now))
                .Select(a => a.UserId)
                .Distinct());
        }

        // The email address itself lives in AuthService; MOD-0024 does not hold a user directory. Until a
        // resolver seam exists, the recipient identity is the user id and the notification layer resolves it.
        return userIds
            .Where(id => id != Guid.Empty)
            .Select(id => new EmailRecipientDto(id.ToString(), null))
            .ToList();
    }

    private async Task<Guid?> ResolveUnitForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var assignments = await _positionAssignments.GetAllAsync(ct);
        var active = assignments
            .Where(a => a.UserId == userId
                        && !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            // A primary assignment is the person's "home" unit when they hold several.
            .OrderBy(a => a.AssignmentType)
            .ToList();

        foreach (var assignment in active)
        {
            var position = await _positions.GetByIdAsync(assignment.PositionId, ct);
            if (position is not null)
            {
                return position.OrganizationUnitId;
            }
        }

        return null;
    }

    /// <summary>
    /// The tenant's root organization unit, used when the assignee holds no active position (a very common state
    /// for administrators and new joiners, who would otherwise be unable to create any task at all).
    ///
    /// <para>"Root" means <c>ParentOrganizationUnitId is null</c> — there is no explicit "is default" flag on
    /// OrganizationUnit. A tenant can legitimately have SEVERAL roots (one per legal entity), so the choice is
    /// made deterministic rather than left to storage order: an <c>HQ</c>-typed root wins, then the lowest
    /// <c>Code</c> ordinally. Same data in, same unit out, on every node.</para>
    /// </summary>
    private async Task<Guid?> ResolveTenantRootUnitAsync(CancellationToken ct)
    {
        var units = await _organizationUnits.GetAllAsync(ct);

        var root = units
            .Where(u => u.ParentOrganizationUnitId is null
                        && !u.IsArchived
                        && u.Status == OrgUnitStatus.Active)
            .OrderByDescending(u => u.OrgUnitType == OrgUnitType.HQ)
            .ThenBy(u => u.Code, StringComparer.Ordinal)
            .FirstOrDefault();

        return root?.Id;
    }

    private static Response<Guid> Fail(string message, string reasonCode, string correlationId)
        => Response<Guid>.Fail(message, 400, reasonCode, correlationId);
}
