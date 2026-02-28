using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// GET /health — public, tenant header gerekmez.
/// Load balancer / liveness probe için.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Get()
        => Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow });
}
