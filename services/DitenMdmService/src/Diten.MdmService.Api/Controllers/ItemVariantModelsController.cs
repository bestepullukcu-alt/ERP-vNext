using Diten.MdmService.Application.Features.ItemVariantModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/item-variant-models")]
public sealed class ItemVariantModelsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemVariantModelsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllItemVariantModelsQuery());
        return Ok(new { data = result });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetItemVariantModelByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemVariantModelRequest request)
    {
        var id = await _mediator.Send(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateItemVariantModelRequest request)
    {
        request.Id = id;
        var updated = await _mediator.Send(request);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteItemVariantModelRequest(id));
        return NoContent();
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteItemVariantModelsRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }
}
