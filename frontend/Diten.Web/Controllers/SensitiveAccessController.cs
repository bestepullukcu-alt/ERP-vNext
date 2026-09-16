using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("HumanCapital/SensitiveAccess")]
public sealed class SensitiveAccessController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/SensitiveAccess/Index.cshtml");
}
