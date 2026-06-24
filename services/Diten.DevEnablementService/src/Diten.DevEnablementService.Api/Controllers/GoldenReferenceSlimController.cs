using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;
using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Queries;
using Diten.DevEnablementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DevEnablementService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/golden-reference-slim")]
public sealed class GoldenReferenceSlimController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GoldenReferenceSlimController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("goldenslim.records.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceSlimListQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("goldenslim.records.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceSlimByIdQuery(id), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("goldenslim.records.create")]
    public async Task<IActionResult> Create([FromBody] CreateGoldenReferenceSlimCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("goldenslim.records.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGoldenReferenceSlimCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("goldenslim.records.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteGoldenReferenceSlimCommand(id), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("bulk")]
    [HasPermission("goldenslim.records.delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteGoldenReferenceSlimCommand(ids), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
