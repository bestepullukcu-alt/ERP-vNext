using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Check()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "Diten.Platform",
            Timestamp = DateTime.UtcNow
        });
    }
}
