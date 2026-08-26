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
[Route("api/finished-goods")]
public sealed class FinishedGoodsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public FinishedGoodsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.finished-goods.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetFinishedGoodsQuery query,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.finished-goods.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetFinishedGoodByIdQuery(id), cancellationToken));

    [HttpGet("gsku-selector")]
    [HasPermission("mdm.finished-goods.create")]
    public async Task<IActionResult> GetGskuSelector(
        [FromQuery] GetFinishedGoodGskuSelectorQuery query,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.finished-goods.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateFinishedGoodDraftCommand(request), cancellationToken));
}
