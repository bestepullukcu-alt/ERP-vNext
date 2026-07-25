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

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(TaskPermissions.Cancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] TaskTransitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new TransitionTaskItemCommand(id, TaskLifecycle.Cancelled, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Positions a task may be pooled to. Carries the organization unit code+name so the picker can render
    /// "QA Specialist — Facility A"; without that label pooled work silently reaches the wrong facility.
    /// </summary>
    [HttpGet("lookups/assignable-positions")]
    [HasPermission(TaskPermissions.Create)]
    public async Task<IActionResult> GetAssignablePositions(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTaskAssignmentPositionLookupQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
