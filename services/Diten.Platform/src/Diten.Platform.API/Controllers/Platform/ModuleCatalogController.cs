using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Application.Features.Tenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/module-catalog")]
[Authorize(Policy = "PlatformActor")]
public sealed class ModuleCatalogController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ModuleCatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetItems([FromQuery] ModuleCatalogFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogItemsQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("stats")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogStatsQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("assignable")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetAssignable(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetAssignableModuleCatalogItemsQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogItemByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("by-code/{moduleCode}")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetByCode(string moduleCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogItemByCodeQuery(moduleCode), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("platform.module-catalog.create")]
    public async Task<IActionResult> Create([FromBody] CreateModuleCatalogItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateModuleCatalogItemCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleCatalogItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateModuleCatalogItemCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/activate")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ActivateModuleCatalogItemCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeactivateModuleCatalogItemCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.module-catalog.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteModuleCatalogItemCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("bulk")]
    [HasPermission("platform.module-catalog.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var response = await _mediator.Send(new BulkDeleteModuleCatalogItemsCommand(ids), ct);
        return CreateActionResultInstance(response);
    }
}
