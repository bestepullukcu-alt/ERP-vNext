using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// MOD-0024 — edit a task's descriptive/planning fields. Assignment target, lifecycle and effort actuals are
/// deliberately NOT editable here: they move through their own commands so each keeps its own guard rails.
/// </summary>
public sealed class UpdateTaskItemHandler : IRequestHandler<UpdateTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskFieldDefinitionService _fieldDefinitions;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITaskApprovalService _approvals;
    private readonly ILogger<UpdateTaskItemHandler> _logger;

    public UpdateTaskItemHandler(
        ITaskItemRepository tasks,
        IOrganizationUnitRepository organizationUnits,
        ITaskFieldDefinitionService fieldDefinitions,
        ICurrentUserContext currentUser,
        ITaskApprovalService approvals,
        ILogger<UpdateTaskItemHandler> logger)
    {
        _tasks = tasks;
        _organizationUnits = organizationUnits;
        _fieldDefinitions = fieldDefinitions;
        _currentUser = currentUser;
        _approvals = approvals;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(UpdateTaskItemCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // A closed task is read-only — a terminal item exposes no state-changing action.
        if (task.CompletedAt is not null || task.CancelledAt is not null)
        {
            return Response<NoContent>.Fail(
                "A closed task cannot be edited.", 409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        if (request.OrganizationUnitId is { } unitId && unitId != task.OrganizationUnitId)
        {
            var unit = await _organizationUnits.GetByIdAsync(unitId, ct);
            if (unit is null || unit.IsArchived)
            {
                return Response<NoContent>.Fail(
                    "The organization unit could not be resolved.",
                    400, TaskReasonCodes.OrganizationUnitUnresolved, command.CorrelationId);
            }

            task.OrganizationUnitId = unitId;
        }

        var fields = await _fieldDefinitions.ValidateAndMaterializeAsync(request.FieldValues, ct);
        if (!fields.IsValid)
        {
            return Response<NoContent>.Fail(
                fields.Message ?? "Invalid task field value.",
                400, fields.ReasonCode ?? TaskReasonCodes.FieldValueInvalid, command.CorrelationId);
        }

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Priority = request.Priority;
        task.DueAt = request.DueAt;
        task.StartAt = request.StartAt;
        task.PlannedDate = request.PlannedDate;
        task.EstimateHours = request.EstimateHours;
        task.Tags = (request.Tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList();
        task.ReviewRequired = request.ReviewRequired;
        task.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        task.DelegationAllowed = request.DelegationAllowed;
        task.FieldValues = fields.Values.ToList();
        task.UpdatedBy = _currentUser.ActorName;
        // SpentHours is untouched: it only moves through execution/time entry, never an edit form (pack §12 Y1).

        // ── Approval toggled by an edit (pack §12 K2, charter Binding A) ──────
        // false→true starts a MOD-0023 instance, true→false cancels the running one. Both are decided here but
        // ACTED ON after the write succeeds, so a workflow is never started or cancelled for an edit that then
        // lost a concurrency race.
        var startApproval = false;
        var cancelApproval = false;
        if (request.ApprovalRequired is { } wantsApproval && wantsApproval != task.ApprovalRequired)
        {
            if (wantsApproval)
            {
                // The manager may already be on the task from an earlier round; only a task with neither is invalid.
                var manager = request.ApprovalManagerUserId ?? task.ApprovalManagerUserId;
                if (manager is null || manager == Guid.Empty)
                {
                    return Response<NoContent>.Fail(
                        "An approval manager is required when approval is requested.",
                        400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
                }

                task.ApprovalRequired = true;
                task.ApprovalManagerUserId = manager;
                startApproval = true;
            }
            else
            {
                // ApprovalRequired goes false now; WorkflowInstanceId is kept until the cancel below has used it.
                task.ApprovalRequired = false;
                cancelApproval = task.WorkflowInstanceId is not null;
            }
        }
        else if (request.ApprovalManagerUserId is { } reassigned
            && task.ApprovalRequired
            && reassigned != Guid.Empty
            && reassigned != task.ApprovalManagerUserId)
        {
            // Re-pointing the approver without touching the requirement: MOD-0023 owns the running instance's
            // assignee, so MOD-0024 records the intent only. Reassignment inside a live approval is Phase 3b.
            task.ApprovalManagerUserId = reassigned;
            _logger.LogInformation(
                "Task {TaskId} approval manager recorded as {ManagerId}; the running MOD-0023 instance keeps its own assignee.",
                task.Id, reassigned);
        }

        if (!await _tasks.UpdateAsync(task, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        if (startApproval)
        {
            // Same contract as creation: a task whose approval could not be started is KEPT with ApprovalRequired
            // true and no instance id, so the fail-closed gate holds `start` shut until it is retried.
            var instanceId = await _approvals.TryStartApprovalAsync(task, ct);
            if (instanceId is not null)
            {
                task.WorkflowInstanceId = instanceId;
                if (!await _tasks.UpdateAsync(task, task.Version, ct))
                {
                    // The approval IS running; only its link failed to persist. Never silent: without the id the
                    // projection cannot read the state, and fail-closed then keeps the task blocked.
                    _logger.LogError(
                        "Approval instance {InstanceId} started for task {TaskId} but the link could not be stored; "
                        + "the task stays blocked until it is retried.", instanceId, task.Id);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Approval was switched on for task {TaskId} but the MOD-0023 instance could not be started; "
                    + "the task stays blocked until it is retried.", task.Id);
            }
        }
        else if (cancelApproval)
        {
            await _approvals.CancelApprovalAsync(task, ct);
            task.WorkflowInstanceId = null;
            if (!await _tasks.UpdateAsync(task, task.Version, ct))
            {
                _logger.LogWarning(
                    "Approval was switched off for task {TaskId} and cancelled, but clearing the instance link failed.",
                    task.Id);
            }
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

public sealed class DeleteTaskItemHandler : IRequestHandler<DeleteTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;

    public DeleteTaskItemHandler(ITaskItemRepository tasks)
    {
        _tasks = tasks;
    }

    public async Task<Response<NoContent>> Handle(DeleteTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // Soft delete (repository base sets IsDeleted); no hard delete anywhere.
        await _tasks.DeleteAsync(command.Id, ct);
        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

public sealed class BulkDeleteTaskItemHandler : IRequestHandler<BulkDeleteTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;

    public BulkDeleteTaskItemHandler(ITaskItemRepository tasks)
    {
        _tasks = tasks;
    }

    public async Task<Response<NoContent>> Handle(BulkDeleteTaskItemCommand command, CancellationToken ct)
    {
        foreach (var id in command.Request.Ids.Distinct())
        {
            // Each delete re-reads through the tenant filter, so an id from another tenant is simply skipped.
            var task = await _tasks.GetByIdAsync(id, ct);
            if (task is not null)
            {
                await _tasks.DeleteAsync(id, ct);
            }
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
