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
    private readonly ITaskSeatDirectory _seats;
    private readonly ITaskFieldDefinitionService _fieldDefinitions;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ITaskApprovalService _approvals;
    private readonly IChecklistTemplateRepository _checklistTemplates;
    private readonly IChecklistRunRepository _checklistRuns;
    private readonly ITaskChecklistService _checklistService;
    private readonly ITaskNotificationService _taskNotifications;
    private readonly ICurrentUserContext _currentUser;
    // BL-023 — is the assignee above me, and if so who carries the request. Neither decides anything.
    private readonly ITaskAssignmentDirection? _direction;
    private readonly ITaskUpwardRequestService? _upwardRequests;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateTaskItemHandler> _logger;

    public CreateTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskWatcherRepository watchers,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        ITaskSeatDirectory seats,
        ITaskFieldDefinitionService fieldDefinitions,
        ITaskLifecycleService lifecycle,
        ITaskApprovalService approvals,
        IChecklistTemplateRepository checklistTemplates,
        IChecklistRunRepository checklistRuns,
        ITaskChecklistService checklistService,
        ITaskNotificationService taskNotifications,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        ILogger<CreateTaskItemHandler> logger,
        /*
         * BL-023 — OPTIONAL on purpose. A caller that supplies neither gets exactly the behaviour that shipped
         * before this change (every assignment is a plain order), which is what the existing suites pin. An
         * absent pair can only ever SKIP the request; it can never open one by accident.
         */
        ITaskAssignmentDirection? direction = null,
        ITaskUpwardRequestService? upwardRequests = null)
    {
        _tasks = tasks;
        _assignments = assignments;
        _watchers = watchers;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _seats = seats;
        _fieldDefinitions = fieldDefinitions;
        _lifecycle = lifecycle;
        _approvals = approvals;
        _checklistTemplates = checklistTemplates;
        _checklistRuns = checklistRuns;
        _checklistService = checklistService;
        _taskNotifications = taskNotifications;
        _currentUser = currentUser;
        _direction = direction;
        _upwardRequests = upwardRequests;
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
        var fields = await _fieldDefinitions.ValidateAndMaterializeAsync(
            request.FieldValues, ct, command.EnforceRequiredFields);
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
            // BL-065 — null stays null: "never chosen" is a real state and means every event, which is what
            // every task did before the preference existed.
            NotifyOnEvents = request.NotifyOnEvents,
            ReminderLeadDays = request.ReminderLeadDays,
            DelegationAllowed = request.DelegationAllowed,
            FieldValues = fields.Values.ToList(),
            CreatedBy = _currentUser.ActorName
        };

        await _tasks.CreateAsync(task, ct);

        /*
         * ── BL-023 — UPWARD work is a REQUEST, not an order ───────────────────
         *
         * A subordinate cannot instruct their own manager. When the assignee sits ABOVE the requester in the
         * reporting chain, a MOD-0023 instance is opened so the manager can accept or refuse — the same handoff
         * approval and review already use, asked as a third question under its own object type.
         *
         * MOD-0024 DECIDES NOTHING here (Binding A): it opens the request, stores the link and moves on. The
         * outcome is read back through the workflow, never resolved locally. Downward and sideways assignments
         * are untouched and stay plain orders.
         *
         * Started after creation for the same reason approval is: the instance references the task id. A
         * request that cannot be opened leaves the task intact — losing work the user already typed is never
         * the better failure.
         */
        if (_direction is not null && _upwardRequests is not null
            && assigneeUserId is { } upwardCandidate
            && await _direction.IsUpwardAsync(upwardCandidate, ct))
        {
            var requestInstanceId = await _upwardRequests.TryStartRequestAsync(task, ct);
            if (requestInstanceId is not null)
            {
                task.RequestWorkflowInstanceId = requestInstanceId;
                await _tasks.UpdateAsync(task, task.Version, ct);
            }
        }

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
    /// Assignment email, through the shared notification service (WC-4).
    ///
    /// <para>This used to be written out here, and it put the recipient's USER ID into the email address field —
    /// the comment said so — so no assignment notification had ever been delivered. The rules that matter (the
    /// task's opt-out, skipping the actor's own action, resolving real addresses, never failing the write) live
    /// in one place now because four events need them.</para>
    /// </summary>
    private async Task NotifyAssignedAsync(TaskItem task, CancellationToken ct)
    {
        // Self-assigned work needs no "you were assigned" email — but that is the SERVICE's actor rule now, so
        // the audience is simply whoever the work went to and the actor filters themselves out.
        var audience = task.AssignmentTarget == TaskAssignmentTarget.PositionPool
            ? await TaskNotificationSafely.ResolvePoolHoldersAsync(_taskNotifications, _logger, task, ct)
            : task.AssigneeUserId is { } assignee ? new[] { assignee } : [];

        await TaskNotificationSafely.NotifyAsync(
            _taskNotifications, _logger, task, TaskNotificationEvents.Assigned, audience, _currentUser.UserId, ct);

        /*
         * Approval requested (WC-4). A task that needs a manager's approval before work may start has an
         * audience of exactly one — the manager — and they have not acted, so they are always told.
         */
        if (task.ApprovalRequired && task.ApprovalManagerUserId is { } manager)
        {
            await TaskNotificationSafely.NotifyAsync(
                _taskNotifications, _logger, task, TaskNotificationEvents.ApprovalRequested,
                new[] { manager }, _currentUser.UserId, ct);
        }
    }

    private async Task<Guid?> ResolveUnitForUserAsync(Guid userId, CancellationToken ct)
    {
        // PRIMARY first — a person holding several seats has one "home", and it decides the task's unit.
        var active = await _seats.ActiveForUserAsync(userId, ct);

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
