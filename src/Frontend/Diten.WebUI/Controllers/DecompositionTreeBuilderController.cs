using Microsoft.AspNetCore.Mvc;

namespace Diten.WebUI.Controllers;

public sealed class DecompositionTreeBuilderController : Controller
{
    [HttpGet]
    public IActionResult Index(string? structureId = null)
    {
        ViewData["StructureId"] = structureId ?? string.Empty;
        return View();
    }
}
