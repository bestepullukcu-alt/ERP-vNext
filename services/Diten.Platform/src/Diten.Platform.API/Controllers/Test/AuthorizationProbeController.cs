using Diten.Platform.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Test;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test/authorization-probe")]
public sealed class AuthorizationProbeController : ControllerBase
{
    private readonly IHostEnvironment environment;

    public AuthorizationProbeController(IHostEnvironment environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    [HttpGet("module-hr")]
    [Authorize]
    [RequiresModule("HR")]
    public IActionResult ModuleHr()
    {
        return Probe("module-hr");
    }

    [HttpGet("feature-advanced-reporting")]
    [Authorize]
    [RequiresFeature("ADVANCED_REPORTING")]
    public IActionResult FeatureAdvancedReporting()
    {
        return Probe("feature-advanced-reporting");
    }

    [HttpGet("module-and-feature")]
    [Authorize]
    [RequiresModule("HR")]
    [RequiresFeature("ADVANCED_REPORTING")]
    public IActionResult ModuleAndFeature()
    {
        return Probe("module-and-feature");
    }

    private IActionResult Probe(string probe)
    {
        if (!IsProbeEnabled())
        {
            return NotFound();
        }

        return Ok(new { ok = true, probe });
    }

    private bool IsProbeEnabled()
    {
        return environment.IsDevelopment()
               || environment.IsEnvironment("Test");
    }
}
