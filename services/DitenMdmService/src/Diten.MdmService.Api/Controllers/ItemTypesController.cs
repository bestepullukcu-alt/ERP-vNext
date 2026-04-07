using Diten.MdmService.Application.Features.ItemLookups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/item-types")]
public sealed class ItemTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetItemTypesQuery());
        return Ok(new { data = result });
    }
}
