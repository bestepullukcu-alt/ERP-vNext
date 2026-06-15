using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Application.Features.ModulePages.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/module-pages")]
[Route("api/platform/module-catalog/pages")]
[Authorize(Policy = "PlatformActor")]
public sealed class ModulePagesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ModulePagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> Search([FromQuery] ModulePageDescriptorFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new SearchModulePageDescriptorsQuery(filter), ct);
        return CreateActionResult(response);
    }

    [HttpGet("by-module/{moduleCode}")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetByModule(string moduleCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModulePageDescriptorsByModuleQuery(moduleCode), ct);
        return CreateActionResult(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModulePageDescriptorByIdQuery(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost]
    [HasPermission("platform.module-catalog.create")]
    public async Task<IActionResult> Create([FromBody] CreateModulePageDescriptorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateModulePageDescriptorCommand(request), ct);
        return CreateActionResult(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModulePageDescriptorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateModulePageDescriptorCommand(id, request), ct);
        return CreateActionResult(response);
    }

    [HttpPost("{id:guid}/activate")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ActivateModulePageDescriptorCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeactivateModulePageDescriptorCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.module-catalog.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteModulePageDescriptorCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpGet("{pageId:guid}/actions")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetActions(Guid pageId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModulePageActionsByPageQuery(pageId), ct);
        return CreateActionResult(response);
    }

    [HttpGet("actions/{id:guid}")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetActionById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModulePageActionByIdQuery(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost("{pageId:guid}/actions")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> CreateAction(Guid pageId, [FromBody] CreateModulePageActionDescriptorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateModulePageActionDescriptorCommand(pageId, request), ct);
        return CreateActionResult(response);
    }

    [HttpPut("actions/{id:guid}")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> UpdateAction(Guid id, [FromBody] UpdateModulePageActionDescriptorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateModulePageActionDescriptorCommand(id, request), ct);
        return CreateActionResult(response);
    }

    [HttpDelete("actions/{id:guid}")]
    [HasPermission("platform.module-catalog.update")]
    public async Task<IActionResult> DeleteAction(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteModulePageActionDescriptorCommand(id), ct);
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
