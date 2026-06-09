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
    [HasPermission("platform.positions.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("platform.positions.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionByIdQuery(id), ct));

    [HttpGet("{id:guid}/manager-chain")]
    [HasPermission("platform.organization.read-manager-chain")]
    public async Task<IActionResult> GetManagerChain(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetManagerChainQuery(id), ct));

    [HttpPost]
    [HasPermission("platform.positions.create")]
    public async Task<IActionResult> Create([FromBody] PositionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePositionCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.positions.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PositionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePositionCommand(id, request), ct));

    [HttpPost("{id:guid}/archive")]
    [HasPermission("platform.positions.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchivePositionCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.positions.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeletePositionCommand(id), ct));
}
