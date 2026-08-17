using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

public sealed class DecompositionTreeBuilderController : Controller
{
    [HttpGet]
    public IActionResult Index(string? structureId = null)
    {
        ViewData["StructureId"] = structureId ?? string.Empty;
        return View();
    }
}
