using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/gskus")]
public sealed class GskusController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GskusController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.gskus.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetGskusQuery query,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.gskus.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(new GetGskuByIdQuery(id), cancellationToken));

    [HttpGet("create-options")]
    [HasPermission("mdm.gskus.create")]
    public async Task<IActionResult> GetCreateOptions(
        [FromQuery] GetGskuCreateOptionsQuery query,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.gskus.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string operationId,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(
            new CreateFirstGskuDraftFacadeCommand(request, operationId),
            cancellationToken));
}
