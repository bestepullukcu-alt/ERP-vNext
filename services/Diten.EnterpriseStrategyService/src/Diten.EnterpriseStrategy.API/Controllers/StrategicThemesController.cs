using Asp.Versioning;
using Diten.Application.Queries.DemandIdeaQueries;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/strategic-themes")]
public sealed class StrategicThemesController : ControllerBase
{
    private readonly IMediator _mediator;

    public StrategicThemesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Diten.Application.Dtos.DemandIdeas.StrategicThemeDto>>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStrategicThemesQuery(), ct);
        return result.Success ? Ok(result.Data) : BadRequest();
    }
}
