using Diten.MdmService.Application.Features.ItemLookups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/unit-of-measures")]
public sealed class UnitOfMeasuresController : ControllerBase
{
    private readonly IMediator _mediator;

    public UnitOfMeasuresController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetUnitOfMeasuresQuery());
        return Ok(new { data = result });
    }
}
