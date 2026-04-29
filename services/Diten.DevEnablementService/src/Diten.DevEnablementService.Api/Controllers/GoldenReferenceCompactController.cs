using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DevEnablementService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/golden-reference-compact")]
public sealed class GoldenReferenceCompactController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GoldenReferenceCompactController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactByIdQuery(id), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoldenReferenceCompactCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGoldenReferenceCompactCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteGoldenReferenceCompactCommand(id), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteGoldenReferenceCompactCommand(ids), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
