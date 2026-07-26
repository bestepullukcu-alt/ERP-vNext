using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

// MOD-0024 Phase 2 — checklist writes. Every one is an expected-version conditional write on the RUN, so two
// people ticking at once produce a controlled 409 rather than one silently overwriting the other.

/// <summary>Tick or untick one item.</summary>
public sealed class SetChecklistItemStateHandler
    : IRequestHandler<SetChecklistItemStateCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IChecklistRunRepository _runs;
    private readonly ITaskChecklistService _checklists;
    private readonly ICurrentUserContext _currentUser;

    public SetChecklistItemStateHandler(
        ITaskItemRepository tasks,
        IChecklistRunRepository runs,
        ITaskChecklistService checklists,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _runs = runs;
        _checklists = checklists;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(SetChecklistItemStateCommand command, CancellationToken ct)
    {
        // The tenant-scoped repository is the cross-tenant guard: another tenant's task does not resolve.
        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var run = await _runs.GetByTaskIdAsync(command.TaskItemId, ct);
        if (run is null)
        {
            return Response<NoContent>.Fail(
                "This task has no checklist.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var item = run.Items.FirstOrDefault(i => i.Code == command.Request.ItemCode);
        if (item is null)
        {
            return Response<NoContent>.Fail(
                "Checklist item not found.", 404, TaskReasonCodes.ChecklistItemNotFound, command.CorrelationId);
        }

        // A finished task's checklist is history — reopening an item would rewrite it.
        if (task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return Response<NoContent>.Fail(
                "A closed task's checklist cannot be changed.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        item.Completed = command.Request.Completed;
        item.CompletedByUserId = command.Request.Completed ? _currentUser.UserId : null;
        item.CompletedAt = command.Request.Completed ? DateTimeOffset.UtcNow : null;
        run.Status = _checklists.ResolveStatus(run);
        run.UpdatedBy = _currentUser.ActorName;

        if (!await _runs.UpdateAsync(run, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The checklist changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Add an ad-hoc item. Its text is stored as TEXT: the user wrote it, so it is not translatable content and must
/// never be treated as a resource key.
/// </summary>
public sealed class AddChecklistItemHandler : IRequestHandler<AddChecklistItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IChecklistRunRepository _runs;
    private readonly ITaskChecklistService _checklists;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public AddChecklistItemHandler(
        ITaskItemRepository tasks,
        IChecklistRunRepository runs,
        ITaskChecklistService checklists,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _runs = runs;
        _checklists = checklists;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(AddChecklistItemCommand command, CancellationToken ct)
    {
        var text = command.Request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Response<NoContent>.Fail(
                "Checklist item text is required.", 400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var run = await _runs.GetByTaskIdAsync(command.TaskItemId, ct);
        if (run is null)
        {
            // First ad-hoc item on a task with no template: start a run for it.
            run = new ChecklistRun
            {
                TenantId = _tenantContext.TenantId,
                TaskItemId = command.TaskItemId,
                CreatedBy = _currentUser.ActorName
            };
            run.Items.Add(NewItem(text, command.Request.Requirement, sortOrder: 0));
            run.Status = _checklists.ResolveStatus(run);
            await _runs.CreateAsync(run, ct);
            return Response<NoContent>.Success(204, command.CorrelationId);
        }

        run.Items.Add(NewItem(text, command.Request.Requirement, run.Items.Count));
        run.Status = _checklists.ResolveStatus(run);
        run.UpdatedBy = _currentUser.ActorName;

        if (!await _runs.UpdateAsync(run, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The checklist changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }

    private static ChecklistRunItem NewItem(string text, ChecklistItemRequirement requirement, int sortOrder)
        => new()
        {
            // A stable per-run code; the text itself is not an identifier.
            Code = $"adhoc-{Guid.NewGuid():N}",
            LabelResourceKey = null,
            LabelText = text,
            Requirement = requirement,
            SortOrder = sortOrder,
            Completed = false
        };
}
