using Diten.MdmService.Application.Features.Products;
using Diten.MdmService.Application.Features.Products.Commands.BulkDeleteProducts;
using Diten.MdmService.Application.Features.Products.Commands.ChangeProductLifecycle;
using Diten.MdmService.Application.Features.Products.Commands.CreateProduct;
using Diten.MdmService.Application.Features.Products.Commands.DeleteProduct;
using Diten.MdmService.Application.Features.Products.Commands.UpdateProduct;
using Diten.MdmService.Application.Features.Products.Queries.GetProductById;
using Diten.MdmService.Application.Features.Products.Queries.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/products")]
public sealed class ProductsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _mediator.Send(new GetProductsQuery());
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _mediator.Send(new GetProductByIdQuery(id));
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var response = await _mediator.Send(command);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command)
    {
        command.Id = id;
        var response = await _mediator.Send(command);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _mediator.Send(new DeleteProductCommand(id));
        return CreateActionResultInstance(response);
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteProductsCommand command)
    {
        var response = await _mediator.Send(command);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{id:guid}/lifecycle")]
    public async Task<IActionResult> ChangeLifecycle(Guid id, [FromBody] ChangeProductLifecycleCommand command)
    {
        command.Id = id;
        var response = await _mediator.Send(command);
        return CreateActionResultInstance(response);
    }
}
