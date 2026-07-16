using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// WorkCenterNext — isolated frontend rebuild of the Work Center (spec:
// docs/workcenter-rebuild-spec.md). Mock-driven, zero backend. The legacy
// /WorkCenter route is left untouched for comparison + rollback.
public class WorkCenterNextController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.ActiveMenu = "workcenternext";
        return View();
    }
}
