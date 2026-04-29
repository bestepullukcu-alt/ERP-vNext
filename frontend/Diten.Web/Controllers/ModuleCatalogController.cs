using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/[controller]")]
public sealed class ModuleCatalogController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/Platform/ModuleCatalog/Index.cshtml");
    }
}
