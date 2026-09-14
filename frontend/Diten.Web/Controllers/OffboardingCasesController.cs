using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("HumanCapital/OffboardingCases")]
public sealed class OffboardingCasesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/OffboardingCases/Index.cshtml");
}
