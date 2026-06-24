using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ModuleDomains;
using Diten.Platform.Application.Features.ModuleDomains.Commands;
using Diten.Platform.Application.Features.ModuleDomains.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/module-domains")]
[Authorize(Policy = "PlatformActor")]
public sealed class ModuleDomainsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ModuleDomainsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.module-domains.read")]
    public async Task<IActionResult> GetItems([FromQuery] ModuleDomainFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleDomainsQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("platform.module-domains.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleDomainByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("platform.module-domains.create")]
    public async Task<IActionResult> Create([FromBody] CreateModuleDomainRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateModuleDomainCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("platform.module-domains.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleDomainRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateModuleDomainCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.module-domains.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteModuleDomainCommand(id), ct);
        return CreateActionResultInstance(response);
    }
}
