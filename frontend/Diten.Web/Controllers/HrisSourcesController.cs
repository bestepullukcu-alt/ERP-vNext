using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/HrisSources")]
public sealed class HrisSourcesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/HrisSources/Index.cshtml");
}
