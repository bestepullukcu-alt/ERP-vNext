using System.Security.Claims;
using Diten.MdmService.Application.Features.Product;
using Diten.MdmService.Application.Features.Product.Commands;
using Diten.MdmService.Application.Features.Product.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0290-FU02 — Product master. As with brands there is NO DELETE action; archiving is a POST to
/// <c>{productId}/archive</c> and never removes Campaign / Knowledge / Frequency references to the product.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mdm/products")]
public sealed class ProductsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("mdm.products.read")]
    public async Task<IActionResult> GetList([FromQuery] GetProductListQuery query, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(query, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{productId:guid}")]
    [HasPermission("mdm.products.read")]
    public async Task<IActionResult> GetById(Guid productId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetProductByIdQuery(productId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("mdm.products.create")]
    public async Task<IActionResult> Create([FromBody] ProductWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new CreateProductCommand(request, Actor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{productId:guid}")]
    [HasPermission("mdm.products.update")]
    public async Task<IActionResult> Update(Guid productId, [FromBody] ProductWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UpdateProductCommand(productId, request, Actor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{productId:guid}/archive")]
    [HasPermission("mdm.products.archive")]
    public async Task<IActionResult> Archive(Guid productId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ArchiveProductCommand(productId, Actor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? Actor =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.Identity?.Name;
}
