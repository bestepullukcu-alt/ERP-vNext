using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.TaskChecklistEngine;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TaskChecklistEngine.Commands;
using Diten.Platform.Application.Features.TaskChecklistEngine.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// MOD-0024 Task &amp; Checklist Engine — generic operational task surfaces (create/assign/complete).
/// Distinct from approval semantics (MOD-0023). Task lifecycle is written to the MOD-0021 audit trail
/// via the audit pipeline behavior (IAuditableCommand).
/// </summary>
[ApiController]
[Route("api/platform/tasks")]
[Authorize(Policy = "PlatformActor")]
public sealed class TasksController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("")]
    [HasPermission("platform.tasks.read")]
    public async Task<IActionResult> List(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "assignee_id")] string? assigneeId,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetWorkTasksQuery(search, status, assigneeId, page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("platform.tasks.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkTaskByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("")]
    [HasPermission("platform.tasks.create")]
    public async Task<IActionResult> Create([FromBody] CreateWorkTaskRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateWorkTaskCommand(
            request.Title, request.Description, request.AssigneeId, request.DueDate,
            request.EscalationPolicy, request.RequiresEvidence), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/assign")]
    [HasPermission("platform.tasks.update")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignWorkTaskRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AssignWorkTaskCommand(id, request.AssigneeId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/complete")]
    [HasPermission("platform.tasks.update")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteWorkTaskRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CompleteWorkTaskCommand(id, request.EvidenceReference), ct);
        return CreateActionResultInstance(response);
    }
}
