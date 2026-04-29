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
    [HasPermission("Modules.ModuleCatalog.Read")]
    public async Task<IActionResult> GetItems([FromQuery] ModuleCatalogFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogItemsQuery(filter), ct);
        return CreateActionResult(response);
    }

    [HttpGet("stats")]
    [HasPermission("Modules.ModuleCatalog.Read")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogStatsQuery(), ct);
        return CreateActionResult(response);
    }

    [HttpGet("assignable")]
    [HasPermission("Modules.ModuleCatalog.Read")]
    public async Task<IActionResult> GetAssignable(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetAssignableModuleCatalogItemsQuery(), ct);
        return CreateActionResult(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("Modules.ModuleCatalog.Read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogItemByIdQuery(id), ct);
        return CreateActionResult(response);
    }

    [HttpGet("by-code/{moduleCode}")]
    [HasPermission("Modules.ModuleCatalog.Read")]
    public async Task<IActionResult> GetByCode(string moduleCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleCatalogItemByCodeQuery(moduleCode), ct);
        return CreateActionResult(response);
    }

    [HttpPost]
    [HasPermission("Modules.ModuleCatalog.Create")]
    public async Task<IActionResult> Create([FromBody] CreateModuleCatalogItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateModuleCatalogItemCommand(request), ct);
        return CreateActionResult(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("Modules.ModuleCatalog.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleCatalogItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateModuleCatalogItemCommand(id, request), ct);
        return CreateActionResult(response);
    }

    [HttpPost("{id:guid}/activate")]
    [HasPermission("Modules.ModuleCatalog.Update")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ActivateModuleCatalogItemCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("Modules.ModuleCatalog.Update")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeactivateModuleCatalogItemCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("Modules.ModuleCatalog.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteModuleCatalogItemCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpDelete("bulk")]
    [HasPermission("Modules.ModuleCatalog.BulkDelete")]
    public async Task<IActionResult> BulkDelete([FromBody] IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var response = await _mediator.Send(new BulkDeleteModuleCatalogItemsCommand(ids), ct);
        return CreateActionResult(response);
    }

    private IActionResult CreateActionResult<T>(Response<T> response)
    {
        var payload = new
        {
            data = response.Data,
            statusCode = response.StatusCode,
            isSuccessful = response.IsSuccessful,
            succeeded = response.IsSuccessful,
            errors = response.Errors,
            message = response.IsSuccessful ? "OK" : string.Join(" ", response.Errors)
        };

        return response.StatusCode switch
        {
            200 => Ok(payload),
            201 => StatusCode(StatusCodes.Status201Created, payload),
            204 => NoContent(),
            400 => BadRequest(payload),
            403 => StatusCode(StatusCodes.Status403Forbidden, payload),
            404 => NotFound(payload),
            409 => Conflict(payload),
            _ => StatusCode(response.StatusCode, payload)
        };
    }
}
