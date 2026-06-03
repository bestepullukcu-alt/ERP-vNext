using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/platform/positions")]
[Authorize]
public sealed class PositionsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PositionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("Modules.Position.Read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("Modules.Position.Read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionByIdQuery(id), ct));

    [HttpGet("{id:guid}/manager-chain")]
    [HasPermission("Modules.Organization.ReadManagerChain")]
    public async Task<IActionResult> GetManagerChain(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetManagerChainQuery(id), ct));

    [HttpPost]
    [HasPermission("Modules.Position.Create")]
    public async Task<IActionResult> Create([FromBody] PositionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePositionCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("Modules.Position.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PositionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePositionCommand(id, request), ct));

    [HttpPost("{id:guid}/archive")]
    [HasPermission("Modules.Position.Archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchivePositionCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission("Modules.Position.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeletePositionCommand(id), ct));
}
