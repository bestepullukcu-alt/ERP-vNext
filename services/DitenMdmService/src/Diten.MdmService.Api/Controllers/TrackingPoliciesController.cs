using Diten.MdmService.Application.Features.ItemLookups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tracking-policies")]
public sealed class TrackingPoliciesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrackingPoliciesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetTrackingPoliciesQuery());
        return Ok(new { data = result });
    }
}
