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
/// MOD-0024 Task &amp; Checklist Engine — checklist template catalog (CRUD) and run execution workspace.
/// Optional evidence is enforced only when a template item is configured as evidence-required.
/// </summary>
[ApiController]
[Route("api/platform/checklists")]
[Authorize(Policy = "PlatformActor")]
public sealed class ChecklistsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ChecklistsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("templates")]
    [HasPermission("platform.checklists.read")]
    public async Task<IActionResult> ListTemplates(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetChecklistTemplatesQuery(search, status, page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("templates/{id:guid}")]
    [HasPermission("platform.checklists.read")]
    public async Task<IActionResult> GetTemplate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetChecklistTemplateByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("templates")]
    [HasPermission("platform.checklists.create")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateChecklistTemplateRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new ChecklistTemplateItemInput(i.Title, i.RequiresEvidence)).ToList();
        var response = await _mediator.Send(new CreateChecklistTemplateCommand(
            request.Code, request.Name, request.Description, items), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("templates/{id:guid}")]
    [HasPermission("platform.checklists.update")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateChecklistTemplateRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new ChecklistTemplateItemInput(i.Title, i.RequiresEvidence)).ToList();
        var response = await _mediator.Send(new UpdateChecklistTemplateCommand(
            id, request.Name, request.Description, request.Status, items, request.ExpectedRowVersion), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("runs")]
    [HasPermission("platform.checklists.read")]
    public async Task<IActionResult> ListRuns(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetChecklistRunsQuery(search, status, page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("runs/{id:guid}")]
    [HasPermission("platform.checklists.read")]
    public async Task<IActionResult> GetRun(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetChecklistRunByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("runs")]
    [HasPermission("platform.checklists.create")]
    public async Task<IActionResult> StartRun([FromBody] StartChecklistRunRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new StartChecklistRunCommand(request.TemplateId, request.Name), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("runs/{runId:guid}/items/{itemId:guid}/complete")]
    [HasPermission("platform.checklists.update")]
    public async Task<IActionResult> CompleteRunItem(Guid runId, Guid itemId, [FromBody] CompleteChecklistRunItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CompleteChecklistRunItemCommand(runId, itemId, request.EvidenceReference), ct);
        return CreateActionResultInstance(response);
    }
}
