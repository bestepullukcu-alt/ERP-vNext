using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/PayrollSources")]
public sealed class PayrollSourcesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/PayrollSources/Index.cshtml");
}
