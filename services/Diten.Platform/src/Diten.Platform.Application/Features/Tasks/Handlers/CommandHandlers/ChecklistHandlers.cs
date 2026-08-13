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

/*
 * ── The three verbs the list was missing ─────────────────────────────────────────────────────────────────────
 *
 * Three of ChecklistRunItem's fields — LabelText, Requirement, EvidenceRequired — were STORED from the moment a
 * task was born and then frozen: no endpoint could ever change them again. The create form let you word an item,
 * level it and flag it for evidence; the task itself let you do none of those things. So the checklist was a
 * decision you made once, before you had done any of the work that would teach you what it should say.
 *
 * Each one below repeats the guards the two existing verbs already established, in the same order, because a
 * checklist write that skips one of them is the divergence this round exists to end:
 *   task exists (the tenant-scoped repository is the cross-tenant guard) → run exists → item exists →
 *   task not closed → conditional write on ExpectedVersion → 409 with a code the caller can act on.
 *
 * What they deliberately DON'T do: write a task_transitions entry. Measured first — neither `add` nor `tick`
 * writes one today, because transitions are recorded by TaskItemRepository.UpdateAsync diffing a TASK, and every
 * checklist write goes to the RUN through a different repository. Three new verbs logging history that the two
 * older ones don't would make the activity feed lie by omission in a NEW way: a reader would see "item removed"
 * and conclude that everything else was untouched. The gap is real and it is now BL-092, for the checklist as a
 * whole rather than for the half of it that happens to be newest.
 */

/// <summary>
/// Edit one item's text, level and evidence flag.
///
/// <para>The text of a TEMPLATE item is refused. Everything else about it is not: see
/// <see cref="UpdateChecklistItemRequest"/> for why that line falls where it does.</para>
/// </summary>
public sealed class UpdateChecklistItemHandler : IRequestHandler<UpdateChecklistItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IChecklistRunRepository _runs;
    private readonly ITaskChecklistService _checklists;
    private readonly ICurrentUserContext _currentUser;

    public UpdateChecklistItemHandler(
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

    public async Task<Response<NoContent>> Handle(UpdateChecklistItemCommand command, CancellationToken ct)
    {
        var found = await ChecklistWriteGuards.ResolveAsync(
            _tasks, _runs, command.TaskItemId, command.ItemCode, command.CorrelationId, ct);
        if (found.Failure is not null) { return found.Failure; }
        var (run, item) = (found.Run!, found.Item!);

        var text = command.Request.LabelText?.Trim();

        // A template item's words are the template's. Refused LOUDLY rather than ignored: silently keeping the
        // old text would tell the caller their edit was saved, and they would find out otherwise on reload.
        if (item.LabelResourceKey is not null && !string.IsNullOrWhiteSpace(text))
        {
            return Response<NoContent>.Fail(
                "This item's text comes from a template and cannot be reworded here.",
                409, TaskReasonCodes.ChecklistItemTemplateOwned, command.CorrelationId);
        }

        // An ad-hoc item with its text erased would render as an empty row nobody can identify or fix.
        if (item.LabelResourceKey is null && string.IsNullOrWhiteSpace(text))
        {
            return Response<NoContent>.Fail(
                "Checklist item text is required.", 400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        if (item.LabelResourceKey is null) { item.LabelText = text; }
        item.Requirement = command.Request.Requirement;
        item.EvidenceRequired = command.Request.EvidenceRequired;

        // Levels decide whether the run is blocked, so the status has to be recomputed after one changes —
        // exactly as ticking an item does.
        run.Status = _checklists.ResolveStatus(run);
        run.UpdatedBy = _currentUser.ActorName;

        return await ChecklistWriteGuards.CommitAsync(
            _runs, run, command.Request.ExpectedVersion, command.CorrelationId, ct);
    }
}

/// <summary>
/// Remove one item.
///
/// <para>A template item CAN be removed, where its text cannot be rewritten — and the two are not in tension.
/// The template says what a job of this kind usually involves; whether a step applies to THIS task is a
/// judgement about this task, and the person holding it is the one placed to make it. Rewording is different:
/// it leaves the item in the list still claiming to be the template's step while saying something else.</para>
/// </summary>
public sealed class RemoveChecklistItemHandler : IRequestHandler<RemoveChecklistItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IChecklistRunRepository _runs;
    private readonly ITaskChecklistService _checklists;
    private readonly ICurrentUserContext _currentUser;

    public RemoveChecklistItemHandler(
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

    public async Task<Response<NoContent>> Handle(RemoveChecklistItemCommand command, CancellationToken ct)
    {
        var found = await ChecklistWriteGuards.ResolveAsync(
            _tasks, _runs, command.TaskItemId, command.ItemCode, command.CorrelationId, ct);
        if (found.Failure is not null) { return found.Failure; }
        var (run, item) = (found.Run!, found.Item!);

        run.Items.Remove(item);

        // Positions are closed up so the list has no hole where the removed row was: a later reorder sends the
        // codes it can see, and a gap here would make the two disagree about what position 3 is.
        var order = 0;
        foreach (var remaining in run.Items.OrderBy(i => i.SortOrder)) { remaining.SortOrder = order++; }

        // Removing the last open blocking item can unblock the run, so the status is recomputed here too.
        run.Status = _checklists.ResolveStatus(run);
        run.UpdatedBy = _currentUser.ActorName;

        return await ChecklistWriteGuards.CommitAsync(
            _runs, run, command.Request.ExpectedVersion, command.CorrelationId, ct);
    }
}

/// <summary>Write the whole order in one conditional write — see <see cref="ReorderChecklistRequest"/>.</summary>
public sealed class ReorderChecklistHandler : IRequestHandler<ReorderChecklistCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IChecklistRunRepository _runs;
    private readonly ICurrentUserContext _currentUser;

    public ReorderChecklistHandler(
        ITaskItemRepository tasks, IChecklistRunRepository runs, ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _runs = runs;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(ReorderChecklistCommand command, CancellationToken ct)
    {
        var found = await ChecklistWriteGuards.ResolveAsync(
            _tasks, _runs, command.TaskItemId, itemCode: null, command.CorrelationId, ct);
        if (found.Failure is not null) { return found.Failure; }
        var run = found.Run!;

        var requested = command.Request.ItemCodes ?? Array.Empty<string>();

        // The submitted order must be a PERMUTATION of what is in the run: same codes, same count, no repeats.
        // Anything less is rejected whole. A caller sending four of five codes has a stale list — applying the
        // four would silently move the fifth to an end it was never dragged to.
        var actual = run.Items.Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
        if (requested.Count != run.Items.Count
            || requested.Distinct(StringComparer.Ordinal).Count() != requested.Count
            || !requested.All(actual.Contains))
        {
            return Response<NoContent>.Fail(
                "The submitted order does not match this checklist; reload and retry.",
                400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        var positions = requested
            .Select((code, index) => (code, index))
            .ToDictionary(x => x.code, x => x.index, StringComparer.Ordinal);
        foreach (var item in run.Items) { item.SortOrder = positions[item.Code]; }

        // Order changes nothing about completion, so the run's status cannot change here — and is left alone
        // rather than recomputed, so that a status bug elsewhere cannot be quietly laundered by a drag.
        run.UpdatedBy = _currentUser.ActorName;

        return await ChecklistWriteGuards.CommitAsync(
            _runs, run, command.Request.ExpectedVersion, command.CorrelationId, ct);
    }
}

/// <summary>
/// The guards every checklist write shares, in one place.
///
/// <para>They were already written three times before this round and are about to be written three more; the
/// version that matters is the CLOSED-TASK check, which the tick verb has and the add verb does not (measured,
/// and reported as BL-093 rather than changed here — it is an existing endpoint's behaviour, not this round's).
/// Having one copy is how the next verb inherits all of them instead of most of them.</para>
/// </summary>
internal static class ChecklistWriteGuards
{
    internal readonly record struct Resolution(
        Response<NoContent>? Failure, ChecklistRun? Run, ChecklistRunItem? Item);

    /// <summary>Task → run → item, with the closed-task refusal. Pass a null itemCode for whole-list writes.</summary>
    internal static async Task<Resolution> ResolveAsync(
        ITaskItemRepository tasks,
        IChecklistRunRepository runs,
        Guid taskItemId,
        string? itemCode,
        string correlationId,
        CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(taskItemId, ct);
        if (task is null)
        {
            return new(Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, correlationId),
                null, null);
        }

        var run = await runs.GetByTaskIdAsync(taskItemId, ct);
        if (run is null)
        {
            return new(Response<NoContent>.Fail(
                "This task has no checklist.", 404, TaskReasonCodes.NotFound, correlationId), null, null);
        }

        // The front end disables these controls on a closed task. That is a courtesy to the reader, not a
        // guard: it is JavaScript on the caller's machine, and the endpoint is reachable without it.
        if (task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return new(Response<NoContent>.Fail(
                "A closed task's checklist cannot be changed.",
                409, TaskReasonCodes.InvalidState, correlationId), null, null);
        }

        if (itemCode is null) { return new(null, run, null); }

        var item = run.Items.FirstOrDefault(i => i.Code == itemCode);
        if (item is null)
        {
            return new(Response<NoContent>.Fail(
                "Checklist item not found.", 404, TaskReasonCodes.ChecklistItemNotFound, correlationId), run, null);
        }

        return new(null, run, item);
    }

    /// <summary>The conditional write and its 409. Never an unconditional save.</summary>
    internal static async Task<Response<NoContent>> CommitAsync(
        IChecklistRunRepository runs,
        ChecklistRun run,
        int expectedVersion,
        string correlationId,
        CancellationToken ct)
        => await runs.UpdateAsync(run, expectedVersion, ct)
            ? Response<NoContent>.Success(204, correlationId)
            : Response<NoContent>.Fail(
                "The checklist changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, correlationId);
}
