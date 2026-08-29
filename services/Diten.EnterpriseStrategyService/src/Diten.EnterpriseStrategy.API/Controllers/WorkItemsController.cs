using Diten.Application.WorkCenter;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.WebAPI.Security;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[Route("api/v1/work-items/projection")]
public sealed class WorkItemsController : ControllerBase
{
    private readonly EnterpriseStrategyWorkItemProjectionService _projection;

    public WorkItemsController(EnterpriseStrategyWorkItemProjectionService projection)
    {
        _projection = projection;
    }

    [HttpGet]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    [ProducesResponseType(typeof(EnterpriseStrategyWorkItemEnvelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EnterpriseStrategyWorkItemEnvelope>> Get(
        [FromQuery] string scope = "self",
        CancellationToken cancellationToken = default)
    {
        var result = await _projection.GetAsync(scope, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
