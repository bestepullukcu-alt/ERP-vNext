using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ModuleServices;
using Diten.Platform.Application.Features.ModuleServices.Commands;
using Diten.Platform.Application.Features.ModuleServices.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/module-services")]
[Authorize(Policy = "PlatformActor")]
public sealed class ModuleServicesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ModuleServicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.module-services.read")]
    public async Task<IActionResult> GetItems([FromQuery] ModuleServiceFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleServicesQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("platform.module-services.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleServiceByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("platform.module-services.create")]
    public async Task<IActionResult> Create([FromBody] CreateModuleServiceRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateModuleServiceCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("platform.module-services.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleServiceRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateModuleServiceCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.module-services.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteModuleServiceCommand(id), ct);
        return CreateActionResultInstance(response);
    }
}
