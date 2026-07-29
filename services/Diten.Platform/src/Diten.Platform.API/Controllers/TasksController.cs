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

    [HttpPost("{id:guid}/plan")]
    [HasPermission(TaskPermissions.Update)]
    public async Task<IActionResult> Plan(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new TransitionTaskItemCommand(id, TaskLifecycle.Planned, request, CorrelationId), ct);
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

    // ── Comments (BL-034 item 7) ─────────────────────────────────────────────

    /// <summary>
    /// Post a comment. Guarded by READ, not Update: commenting is not a change to the work, and the person asking
    /// "why is this still waiting?" is usually not the one holding it.
    ///
    /// <para>There is deliberately no PUT and no DELETE. A comment is immutable — see <c>TaskComment</c>.</para>
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    [HasPermission(TaskPermissions.Read)]
    public async Task<IActionResult> AddComment(
        Guid id, [FromBody] AddTaskCommentRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddTaskCommentCommand(id, request, CorrelationId), ct);
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
        var response = await _mediator.Send(new GetTaskAssignmentPersonLookupQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
