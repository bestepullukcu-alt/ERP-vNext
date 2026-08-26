using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0024 — thin task engine controller. Version-explicit route under <c>api/v1/tasks</c>; gateway routing is a
/// separate integration-agent task.
///
/// <para>The route deliberately avoids <c>api/tasks</c>, which is already owned by the frozen legacy
/// <c>TaskApiController</c> in Diten.Web (it serves the legacy /WorkCenter surface).</para>
///
/// <para>Every action is permission-gated with keys the module MANIFEST declares, so their Module/Scope attribution
/// is authored by the manifest sync rather than the reflection worker (which would stamp them PlatformAdmin and
/// make them unassignable to a tenant role).</para>
/// </summary>
[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public sealed class TasksController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public TasksController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost]
    [HasPermission(TaskPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateTaskItemCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskItemListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskItemByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(TaskPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteTaskItemCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("bulk-delete")]
    [HasPermission(TaskPermissions.BulkDelete)]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new BulkDeleteTaskItemCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Accept a task assigned to me (the Inbox acceptance gate).</summary>
    [HttpPost("{id:guid}/accept")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AcceptTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Take an unclaimed pool task. Concurrent claims resolve to one owner; the loser gets 409.</summary>
    [HttpPost("{id:guid}/claim")]
    [HasPermission(TaskPermissions.Claim)]
    public async Task<IActionResult> Claim(Guid id, [FromBody] ClaimTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ClaimTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/release")]
    [HasPermission(TaskPermissions.Claim)]
    public async Task<IActionResult> Release(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReleaseTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Set or move a personal plan date. Its own request type, not <see cref="TaskTransitionRequest"/> — the date
    /// is required here and this is the one transition that also targets its OWN current state (re-planning a
    /// task that is already Planned), so it is routed through <see cref="PlanTaskItemCommand"/> rather than
    /// <see cref="TransitionTaskItemCommand"/>.
    /// </summary>
    [HttpPost("{id:guid}/plan")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Plan(Guid id, [FromBody] PlanTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new PlanTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/start")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Start(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new TransitionTaskItemCommand(id, TaskLifecycle.InProgress, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Hand finished work to a reviewer. The route segment MUST match the projected action code
    /// (<c>submitReview</c>): the client turns the code straight into the URL, and Diten.Web's proxy has to list
    /// it too — a code missing from either is a 404 on a button the user can see.
    /// </summary>
    [HttpPost("{id:guid}/submitReview")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> SubmitReview(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SubmitTaskForReviewCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/complete")]
    [HasPermission(TaskPermissions.Complete)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new TransitionTaskItemCommand(id, TaskLifecycle.Done, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Say the task is blocked on someone else, with what it is waiting for. The route segment MUST match the
    /// projected action code (<c>inquire</c>): the client turns the code straight into the URL.
    /// </summary>
    [HttpPost("{id:guid}/inquire")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Inquire(Guid id, [FromBody] InquireTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new InquireTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Give assigned work back to whoever asked for it. Route segment MUST match the projected action code.
    ///
    /// <para>MOD-0023 has a <c>return</c> of its own — an approver sending an approval or review back to its
    /// submitter. Same verb, different owner and different work-intent type; the two are deliberately separate
    /// routes and must not be merged (charter Binding A).</para>
    /// </summary>
    [HttpPost("{id:guid}/return")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReturnTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Hand work to a different person. Guarded by ASSIGN, not Update: choosing who does the work is the act of
    /// assigning it, which is a different authority from editing the task.
    /// </summary>
    [HttpPost("{id:guid}/reassign")]
    [HasPermission(TaskPermissions.Assign)]
    public async Task<IActionResult> Reassign(
        Guid id, [FromBody] ReassignTaskItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReassignTaskItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Call the task off. Holding <c>platform.tasks.cancel</c> is not enough on its own — the handler also
    /// requires the caller to be the REQUESTER, or to hold administrative authority over any task. That
    /// authority is evaluated HERE, from the caller's claims through the same seam the enforcement filter uses,
    /// and passed to the handler as data: PermissionClaimEvaluator lives in this layer so the two cannot drift.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(TaskPermissions.Cancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var mayCancelAnyTask = PermissionClaimEvaluator.Evaluate(User.Claims, TaskPermissions.Delete).IsSatisfied;
        var response = await _mediator.Send(
            new TransitionTaskItemCommand(id, TaskLifecycle.Cancelled, request, CorrelationId, mayCancelAnyTask), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Positions a task may be pooled to. Carries the organization unit code+name so the picker can render
    /// "QA Specialist — Facility A"; without that label pooled work silently reaches the wrong facility.
    /// </summary>
    /// <summary>Create a task from a reusable template; its checklist is instantiated too (pack §12 E5).</summary>
    [HttpPost("from-template")]
    [HasPermission(TaskPermissions.Create)]
    public async Task<IActionResult> CreateFromTemplate(
        [FromBody] CreateTaskFromTemplateRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateTaskItemFromTemplateCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── Phase 2: checklist (pack §12 E1) ─────────────────────────────────────

    /// <summary>
    /// Tick or untick a checklist item. Guarded by UPDATE, not COMPLETE: ticking an item is progress on the
    /// task, not the act of finishing it.
    /// </summary>
    [HttpPost("{id:guid}/checklist/items/state")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> SetChecklistItemState(
        Guid id, [FromBody] SetChecklistItemStateRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SetChecklistItemStateCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Add an ad-hoc checklist item (the user's own words — stored as text, never a resource key).</summary>
    [HttpPost("{id:guid}/checklist/items")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> AddChecklistItem(
        Guid id, [FromBody] AddChecklistItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddChecklistItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Edit one checklist item's text, level and evidence flag. UPDATE for the same reason as ticking: this is
    /// progress on the work, not the act of finishing it.
    /// </summary>
    [HttpPut("{id:guid}/checklist/items/{code}")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> UpdateChecklistItem(
        Guid id, string code, [FromBody] UpdateChecklistItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateChecklistItemCommand(id, code, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Remove one checklist item.</summary>
    [HttpDelete("{id:guid}/checklist/items/{code}")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> RemoveChecklistItem(
        Guid id, string code, [FromBody] RemoveChecklistItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RemoveChecklistItemCommand(id, code, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Write the whole checklist order at once. One call rather than one per item — see
    /// <see cref="ReorderChecklistRequest"/> for why per-item writes lose races that this cannot.
    /// </summary>
    [HttpPut("{id:guid}/checklist/order")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> ReorderChecklist(
        Guid id, [FromBody] ReorderChecklistRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReorderChecklistCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── Comments (BL-034 item 7) ─────────────────────────────────────────────

    /*
     * ══ COMMENTS: THE IMMUTABILITY DECISION, AND WHAT REPLACED IT (2026-08-14, owner) ═════════════════════════
     *
     * This block used to read: "There is deliberately no PUT and no DELETE. A comment is immutable — see
     * TaskComment." It is recorded here rather than deleted, because the reasoning behind it was never wrong:
     * changing a sentence somebody has already replied to can turn their reply into nonsense, and in an ERP that
     * is rewriting history.
     *
     * What changed is that the compromise was found — THE TRAIL. Immutability was protecting exactly one property:
     * nothing disappears or changes silently. An edit that SAYS it was edited, and a withdrawal that leaves a
     * marker where the comment stood, both leave that property standing.
     *
     * So a PUT and a DELETE exist now, and three rules hold the old decision's line:
     *   · ONLY THE AUTHOR — no manager exception and no administrator override. Nobody asked for one, and an
     *     authority over other people's words is far easier to grant than to take back.
     *   · DELETE IS A TOMBSTONE — the text is cleared, the row survives, the feed keeps saying somebody spoke
     *     here and withdrew it. There is still no hard delete of a comment anywhere in this module.
     *   · NEITHER SENDS EMAIL — only a NEW comment notifies. A typo correction does not earn anybody's inbox.
     */

    /// <summary>
    /// Post a comment. Guarded by READ, not Update: commenting is not a change to the work, and the person asking
    /// "why is this still waiting?" is usually not the one holding it.
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> AddComment(
        Guid id, [FromBody] AddTaskCommentRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddTaskCommentCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Rewrite one's OWN comment. READ-guarded like posting one, for the same reason: this is not a change to the
    /// work. The AUTHOR check is the real gate and it lives in the handler, where it cannot be bypassed by a
    /// caller who happens to hold a stronger permission.
    /// </summary>
    [HttpPut("{id:guid}/comments/{commentId:guid}")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> UpdateComment(
        Guid id, Guid commentId, [FromBody] UpdateTaskCommentRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new UpdateTaskCommentCommand(id, commentId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Withdraw one's OWN comment. A TOMBSTONE — the text is cleared and the row stays, so the feed keeps a
    /// marker where somebody spoke and took it back.
    /// </summary>
    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> WithdrawComment(Guid id, Guid commentId, CancellationToken ct)
    {
        var response = await _mediator.Send(new WithdrawTaskCommentCommand(id, commentId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── The personal overlay (WC-1) ──────────────────────────────────────────
    //
    // All three are guarded by READ, not Update, and the reason is the same for each: a private note or a snooze
    // changes MY VIEW of the work, never the work. Requiring Update would stop the reader who may look at a task
    // but not move it from leaving themselves a reminder about it — the very reader who most needs one.
    //
    // Whose overlay is never a parameter. The caller's identity comes from ICurrentUserContext inside the
    // handlers, so there is no request shape in which one user can read or write another's notes.

    /// <summary>Add one private note to a task. The note is visible to its author and to nobody else.</summary>
    [HttpPost("{id:guid}/personal/notes")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> AddPersonalNote(
        Guid id, [FromBody] AddTaskPersonalNoteRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddTaskPersonalNoteCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Delete one of the caller's own notes. Anyone else's id answers 404 — see the command for why.</summary>
    [HttpDelete("{id:guid}/personal/notes/{noteId:guid}")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> DeletePersonalNote(Guid id, Guid noteId, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteTaskPersonalNoteCommand(id, noteId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Park this task in the caller's own inbox until a date, or wake it now by sending a null date. The task
    /// itself does not move: no lifecycle, no status, no waiting context.
    /// </summary>
    [HttpPut("{id:guid}/personal/snooze")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> SetSnooze(
        Guid id, [FromBody] SetTaskSnoozeRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SetTaskSnoozeCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Pin or unpin this task for the caller. Like the snooze above, the task itself does not move: no
    /// lifecycle, no status, no waiting context, and nothing the requester can observe.
    /// </summary>
    [HttpPut("{id:guid}/personal/pin")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> SetPinned(
        Guid id, [FromBody] SetTaskPinnedRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SetTaskPinnedCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── Dependencies (BL-028, pack §12 Y3) ───────────────────────────────────

    /// <summary>
    /// Add a typed dependency between two of this module's own tasks. Guarded by UPDATE: an edge changes what the
    /// task may DO next, which is a change to the task, not the creation of a new record.
    /// </summary>
    [HttpPost("{id:guid}/dependencies")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> AddDependency(
        Guid id, [FromBody] AddTaskDependencyRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddTaskDependencyCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Remove one dependency edge from this task.</summary>
    [HttpDelete("{id:guid}/dependencies/{dependencyId:guid}")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> RemoveDependency(Guid id, Guid dependencyId, CancellationToken ct)
    {
        var response = await _mediator.Send(new RemoveTaskDependencyCommand(id, dependencyId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── Configurable field definitions (Phase 5) ─────────────────────────────
    //
    // Their own resource under tasks, guarded by the manage permission the pack declares — reading the catalogue
    // is an ordinary task read, but shaping it is an administrative act.
    //
    // Each route has to be listed on the Diten.Web proxy too: one that exists here and not there answers 404
    // before the request leaves the web tier, which is how `inquire` shipped unreachable.

    // ── DCP-005 slice 2: the controlled-document reference list ──────────

    /// <summary>
    /// Read the register WITHOUT storing it: how many rows, how many citable, which columns are unread, and
    /// whether these exact bytes are already a stored version. Same two-step the folder taxonomy import uses.
    /// </summary>
    [HttpPost("document-list/dry-run")]
    [HasPermission(TaskPermissions.DocumentListImport)]
    public async Task<IActionResult> DryRunDocumentList(
        [FromBody] ImportDocumentReferenceListRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DryRunDocumentReferenceListCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Store the register as a new list VERSION.</summary>
    [HttpPost("document-list/import")]
    [HasPermission(TaskPermissions.DocumentListImport)]
    public async Task<IActionResult> ImportDocumentList(
        [FromBody] ImportDocumentReferenceListRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ImportDocumentReferenceListCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Every import, newest first — "which list did this task resolve against".</summary>
    [HttpGet("document-list/versions")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetDocumentListVersions(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetDocumentReferenceListVersionsQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Search the current list.
    ///
    /// ⚠ Guarded by <c>Read</c>, like the task-type picker and for the same reason: citing a procedure is
    /// ordinary work, importing the register is not.
    /// </summary>
    [HttpGet("document-list/search")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> SearchDocumentList(
        [FromQuery] string? term, [FromQuery] int limit, CancellationToken ct)
    {
        var response = await _mediator.Send(new SearchDocumentReferencesQuery(term, limit, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── DCP-005 slice 1: task types ──────────────────────────────────────

    /// <summary>
    /// Every task type, retired ones included — the management screen. Guarded by the MANAGE permission because
    /// it shows retired types and the fields behind them; the task form uses <see cref="GetActiveTaskTypes"/>.
    /// </summary>
    [HttpGet("task-types")]
    [HasPermission(TaskPermissions.TaskTypesManage)]
    public async Task<IActionResult> GetTaskTypes(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskTypeListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Types a NEW task may be given.
    ///
    /// ⚠ Guarded by <c>Read</c> ON PURPOSE. Anyone who can create a task must be able to choose its type; what
    /// they cannot do is create one. That split is what QA's control statement rests on — see
    /// <c>TaskPermissions.TaskTypesManage</c>.
    /// </summary>
    [HttpGet("task-types/active")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetActiveTaskTypes(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetActiveTaskTypesQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("task-types/{id:guid}")]
    [HasPermission(TaskPermissions.TaskTypesManage)]
    public async Task<IActionResult> GetTaskType(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskTypeByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("task-types")]
    [HasPermission(TaskPermissions.TaskTypesManage)]
    public async Task<IActionResult> CreateTaskType(
        [FromBody] CreateTaskTypeRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateTaskTypeCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Edit a type. The code is read-only and a differing one is refused, never ignored.</summary>
    [HttpPut("task-types/{id:guid}")]
    [HasPermission(TaskPermissions.TaskTypesManage)]
    public async Task<IActionResult> UpdateTaskType(
        Guid id, [FromBody] UpdateTaskTypeRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateTaskTypeCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Retire or restore a type.
    ///
    /// ⚠ THERE IS NO DELETE ROUTE, deliberately — a used type is part of the identity of every task opened under
    /// it. See <c>SetTaskTypeActiveHandler</c>.
    /// </summary>
    [HttpPut("task-types/{id:guid}/active")]
    [HasPermission(TaskPermissions.TaskTypesManage)]
    public async Task<IActionResult> SetTaskTypeActive(
        Guid id, [FromBody] SetTaskTypeActiveRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SetTaskTypeActiveCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("field-definitions")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetFieldDefinitions(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskFieldDefinitionListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// The option list one configurable field offers, resolved from the source ITS OWN definition names.
    ///
    /// <para>An ordinary task READ, not the manage permission: a user who may create a task has to be able to
    /// fill the fields they are asked for. Gating this behind
    /// <see cref="TaskPermissions.FieldDefinitionsManage"/> would leave every ordinary user with an empty
    /// picker — and an unfillable selector is the same class of defect as a payload nobody reads.</para>
    ///
    /// <para>The route takes a CODE, not an id, and never a lookup key or a set code: the definition is the
    /// allow-list, so this cannot be used to read a reference set no field points at.</para>
    /// </summary>
    [HttpGet("field-definitions/{code}/options")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetFieldDefinitionOptions(string code, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskFieldDefinitionOptionsQuery(code, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// The records ONE configurable field offers, searched in the module that owns them.
    ///
    /// <para>Same permission and same reasoning as the options route it sits beside: filling a field you were
    /// asked to fill is an ordinary task read. Same allow-list too — the caller names a FIELD, and the definition
    /// decides which source is reachable, so this cannot be turned into a general "search any module" endpoint.
    /// </para>
    ///
    /// <para>It answers the same <c>TaskFieldOptionDto</c> the options route does, because a picker must not have
    /// to know which kind of source filled it. <c>ids</c> is the EDIT path: the identities already on a task,
    /// resolved back into records the form can display.</para>
    /// </summary>
    [HttpGet("field-definitions/{code}/records")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> SearchFieldDefinitionRecords(
        string code,
        [FromQuery] string? term,
        [FromQuery] string? ids,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        var identities = string.IsNullOrWhiteSpace(ids)
            ? null
            : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var response = await _mediator.Send(
            new GetTaskFieldDefinitionOptionsQuery(code, CorrelationId, term, identities, take), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// The sources an administrator may point a field at, for the kind they picked — what the field-definition
    /// screen offers instead of a free-text box, so a key can no longer be mistyped into a field that silently
    /// never appears.
    ///
    /// <para>The MANAGE permission, unlike the two routes above: this is the shaping act, not the filling one.
    /// </para>
    /// </summary>
    [HttpGet("field-definitions/option-sources")]
    [HasPermission(TaskPermissions.FieldDefinitionsManage)]
    public async Task<IActionResult> GetFieldOptionSources(
        [FromQuery] TaskFieldOptionsSourceKind kind, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskFieldOptionSourcesQuery(kind, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("field-definitions/{id:guid}")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> GetFieldDefinition(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskFieldDefinitionByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("field-definitions")]
    [HasPermission(TaskPermissions.FieldDefinitionsManage)]
    public async Task<IActionResult> CreateFieldDefinition(
        [FromBody] CreateTaskFieldDefinitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateTaskFieldDefinitionCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Edit a definition. The request carries no <c>Code</c>: values already stored join to their definition by
    /// code, so an edited code orphans them all.
    /// </summary>
    [HttpPut("field-definitions/{id:guid}")]
    [HasPermission(TaskPermissions.FieldDefinitionsManage)]
    public async Task<IActionResult> UpdateFieldDefinition(
        Guid id, [FromBody] UpdateTaskFieldDefinitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateTaskFieldDefinitionCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Retire several definitions. POST with an envelope, matching this controller's existing
    /// <c>bulk-delete</c> rather than the bare-array DELETE the list script inherited from the golden reference:
    /// two bulk shapes in one controller costs more than adapting the client, and a body on DELETE is the shape
    /// proxies treat least predictably.
    ///
    /// <para>The SAME permission as the single retire. Doing it to several at once is not a higher authority,
    /// and a separate key would be one more thing to grant and forget.</para>
    /// </summary>
    [HttpPost("field-definitions/bulk-delete")]
    [HasPermission(TaskPermissions.FieldDefinitionsManage)]
    public async Task<IActionResult> BulkDeleteFieldDefinitions(
        [FromBody] BulkDeleteTaskFieldDefinitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new BulkDeleteTaskFieldDefinitionCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Retire a definition. Always a deactivation — never a destruction (see the handler).</summary>
    [HttpDelete("field-definitions/{id:guid}")]
    [HasPermission(TaskPermissions.FieldDefinitionsManage)]
    public async Task<IActionResult> DeleteFieldDefinition(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteTaskFieldDefinitionCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── Recurrence rules (Phase 4) ───────────────────────────────────────────
    //
    // Their own resource under tasks, not transition codes. The Diten.Web proxy has to carry each of these
    // explicitly: a route that exists here and not there answers 404 before the request ever leaves the web
    // tier, which is exactly how `inquire` shipped unreachable.
    //
    // The SWEEP that acts on these rules is off unless BackgroundJobs:RegisterStandardJobs and
    // EnabledJobs["Diten.Platform.MOD-0024.TaskRecurrenceSweepJob"] are BOTH true. A rule created here does
    // nothing until then, and that is configuration rather than a defect.

    [HttpGet("recurrence-rules")]
    [HasPermission(TaskPermissions.RecurrenceManage)]
    public async Task<IActionResult> GetRecurrenceRules(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskRecurrenceRuleListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("recurrence-rules/{id:guid}")]
    [HasPermission(TaskPermissions.RecurrenceManage)]
    public async Task<IActionResult> GetRecurrenceRule(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskRecurrenceRuleByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("recurrence-rules")]
    [HasPermission(TaskPermissions.RecurrenceManage)]
    public async Task<IActionResult> CreateRecurrenceRule(
        [FromBody] CreateTaskRecurrenceRuleRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateTaskRecurrenceRuleCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("recurrence-rules/{id:guid}")]
    [HasPermission(TaskPermissions.RecurrenceManage)]
    public async Task<IActionResult> UpdateRecurrenceRule(
        Guid id, [FromBody] UpdateTaskRecurrenceRuleRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateTaskRecurrenceRuleCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("recurrence-rules/{id:guid}")]
    [HasPermission(TaskPermissions.RecurrenceManage)]
    public async Task<IActionResult> DeleteRecurrenceRule(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteTaskRecurrenceRuleCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("lookups/assignable-positions")]
    [HasPermission(TaskPermissions.Create)]
    public async Task<IActionResult> GetAssignablePositions(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskAssignmentPositionLookupQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// People a task may be assigned to — whoever currently holds a position (pack §12 K6.4). Carries the
    /// position and its organization unit for the same reason as above: two people holding "QA Specialist" in
    /// different facilities are otherwise indistinguishable.
    ///
    /// <para>Guarded by the ASSIGN permission, not Create: reading who can receive work is exactly the act of
    /// assigning it. AuthService's <c>auth.users.read</c> is granted to nobody for this — Platform resolves the
    /// names service-to-service.</para>
    /// </summary>
    [HttpGet("lookups/assignable-people")]
    [HasPermission(TaskPermissions.Assign)]
    public async Task<IActionResult> GetAssignablePeople(CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetTaskAssignmentPersonLookupQuery(CorrelationId, TaskPersonLookupPurpose.Assignment), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// People who may DECIDE about a task — the approver and the reviewer (BL-057).
    ///
    /// <para>Deliberately NOT the same list as <c>assignable-people</c>, and this is the endpoint that exists so
    /// the difference cannot be lost. Assignment is limited to the actor's company scope; approval authority is
    /// not, because it belongs to the PROCESS rather than to the requester. A box produced in GMG TR is
    /// legitimately approved in GMG AZ by somebody who is neither above nor below the author — every leg of the
    /// assignment rule fails for them, and the work is still entirely proper. Serving both lists from one
    /// endpoint would have meant one filter for four pickers, which silently kills intra-group approval.</para>
    ///
    /// <para>Guarded by CREATE rather than ASSIGN: choosing who approves is part of defining the task, not of
    /// handing work to anyone. The list still only contains people holding a live position in this tenant —
    /// "exempt" means exempt from the COMPANY scope, not from every boundary.</para>
    /// </summary>
    /// <summary>
    /// BL-023 — would assigning to this person be an UPWARD request rather than an order?
    ///
    /// <para>Asked by the create form so the button can read "Talep gönder" BEFORE the user presses it: a
    /// control that silently behaves differently from its own label is the defect this project keeps
    /// correcting. The answer comes from the same ManagerChain scope the server uses when it actually opens the
    /// request, so the label and the behaviour cannot disagree.</para>
    /// </summary>
    [HttpGet("lookups/assignment-direction/{userId:guid}")]
    [HasPermission(TaskPermissions.Create)]
    public async Task<IActionResult> GetAssignmentDirection(Guid userId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskAssignmentDirectionQuery(userId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("lookups/decision-makers")]
    [HasPermission(TaskPermissions.Create)]
    public async Task<IActionResult> GetDecisionMakers(CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetTaskAssignmentPersonLookupQuery(CorrelationId, TaskPersonLookupPurpose.Decision), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Templates a recurrence rule can be bound to (BL-052). Guarded by Create, because binding a template is
    /// part of defining what gets created — the same permission the rule endpoints below already require.
    /// </summary>
    [HttpGet("lookups/task-templates")]
    [HasPermission(TaskPermissions.RecurrenceManage)]
    public async Task<IActionResult> GetTaskTemplates(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskTemplateLookupQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
