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
[Route("api/global-products")]
public sealed class GlobalProductsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GlobalProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.global-products.read")]
    public async Task<IActionResult> GetAll([FromQuery] GetGlobalProductsQuery query, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("selector")]
    [HasPermission("mdm.global-products.read")]
    public async Task<IActionResult> GetSelector(
        [FromQuery] GetGlobalProductSelectorQuery query,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.global-products.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetGlobalProductByIdQuery(id), cancellationToken));

    [HttpPost("code-reservations")]
    [HasPermission("mdm.global-products.create")]
    public async Task<IActionResult> ReserveCode(
        [FromBody] ProductItemSkuMasterModels.ReserveGlobalProductCodeRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ReserveCanonicalCodeCommand(request), cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.global-products.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] ProductItemSkuMasterModels.CreateGlobalProductDraftRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateGlobalProductDraftCommand(request), cancellationToken));
}
