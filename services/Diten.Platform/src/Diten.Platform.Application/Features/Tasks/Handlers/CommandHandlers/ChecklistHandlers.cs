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

        /*
         * BL-093 — a closed task's checklist is history here too.
         *
         * Four of this card's five verbs refused a Done or Cancelled task and this one accepted, so a finished
         * task could still grow new steps. Not written through ChecklistWriteGuards.ResolveAsync like the others,
         * because this verb legitimately runs when there is NO run yet — the resolver's "this task has no
         * checklist" 404 is correct for the other four and wrong for the one that creates it.
         */
        if (task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return Response<NoContent>.Fail(
                "A closed task's checklist cannot be changed.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
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

    private ChecklistRunItem NewItem(string text, ChecklistItemRequirement requirement, int sortOrder)
        => new()
        {
            // A stable per-run code; the text itself is not an identifier.
            Code = $"adhoc-{Guid.NewGuid():N}",
            LabelResourceKey = null,
            LabelText = text,
            Requirement = requirement,
            SortOrder = sortOrder,
            Completed = false,
            /*
             * WHO PUT IT THERE, stamped at the only moment it is knowable for free. Nothing had to be plumbed
             * for this: the identity was already injected here to fill CreatedBy. The rule that needs it —
             * only the author may re-level or remove a step — was unwritable purely because nobody wrote this
             * one line at the time.
             */
            AddedByUserId = _currentUser.UserId,
            AddedAt = DateTimeOffset.UtcNow
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

        /*
         * WHOSE STEP IS THIS — asked before anything else, because the answer decides all three fields at once.
         *
         * A round ago the level and the evidence flag were deliberately left open on a template item, on the
         * reasoning that "how strictly THIS task is run is the holder's judgement". That reasoning was wrong in
         * the one case that matters: dropping Blocking → Optional releases the gate just as completely as
         * deleting the item, so a rule that protects the words and not the level protects nothing. Reversed.
         */
        var refusal = ChecklistWriteGuards.RefuseNotYours(item, _currentUser.UserId, command.CorrelationId);
        if (refusal is not null) { return refusal; }

        // Anything reaching here is the caller's OWN ad-hoc item — template rows were turned away above — so the
        // text is theirs to change, and the only thing left to refuse is erasing it: a blank row is
        // unidentifiable, and so unfixable by the next reader.
        var text = command.Request.LabelText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Response<NoContent>.Fail(
                "Checklist item text is required.", 400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        item.LabelText = text;
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
/// Remove one item — YOUR item.
///
/// <para>This used to say a template item could be removed even though its text could not be rewritten, on the
/// reasoning that whether a step applies to this task is the holder's judgement. REVERSED, and the reversal is
/// the point of this round: the item most worth removing is the blocking one standing between the holder and
/// "done", so "the holder decides which steps apply" hands them the key to every gate on their own task.</para>
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

        // The gate cannot be lifted by the person it is holding. Same rule as the edit, and for a sharper
        // reason: removing the last open blocking item unblocks the run outright — this handler's own status
        // recomputation below says so.
        var refusal = ChecklistWriteGuards.RefuseNotYours(item, _currentUser.UserId, command.CorrelationId);
        if (refusal is not null) { return refusal; }

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

    /// <summary>
    /// May this caller CHANGE this item — reword it, re-level it, re-flag it, remove it?
    ///
    /// <para>Only its author. Ticking is deliberately not routed through here: doing the work is everyone's, and
    /// a checklist you may not tick is not a checklist.</para>
    ///
    /// <para>A null author is SOMEBODY ELSE'S. Two kinds of item arrive that way — rows written before the field
    /// existed, and rows instantiated from a template, which has no author because the template is the author —
    /// and the safe answer is the same for both. The asymmetry is the whole argument: refusing an edit that
    /// should have been allowed produces a complaint, and allowing a deletion that should have been refused
    /// removes a gate silently, discovered only after whatever the gate existed to prevent has happened.</para>
    /// </summary>
    internal static Response<NoContent>? RefuseNotYours(
        ChecklistRunItem item, Guid? callerUserId, string correlationId)
    {
        /*
         * TWO codes for two genuinely different sentences, and the SPECIFIC one is checked first.
         *
         * A template item never has an author, so an author-only check would answer "somebody else added this"
         * for every template step — true in effect, and useless to the reader, who wants to know that the step
         * comes from the process rather than from a colleague they could go and ask.
         */
        if (item.LabelResourceKey is { Length: > 0 })
        {
            return Response<NoContent>.Fail(
                "This item comes from a template and cannot be changed on a single task.",
                409, TaskReasonCodes.ChecklistItemTemplateOwned, correlationId);
        }

        return item.AddedByUserId is not null && item.AddedByUserId == callerUserId
            ? null
            : Response<NoContent>.Fail(
                "This checklist item was added by someone else and cannot be changed here.",
                409, TaskReasonCodes.ChecklistItemNotAuthor, correlationId);
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
