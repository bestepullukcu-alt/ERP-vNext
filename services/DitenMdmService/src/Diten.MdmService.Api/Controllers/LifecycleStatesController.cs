using Diten.MdmService.Application.Features.ItemLookups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/lifecycle-states")]
public sealed class LifecycleStatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LifecycleStatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetLifecycleStatesQuery());
        return Ok(new { data = result });
    }
}
