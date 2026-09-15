using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// Renamed from hr-future's PositionAssignmentsController to avoid a class/file
// collision with main's existing (Platform) PositionAssignmentsController
// (route "PositionAssignments" -> /api/platform/position-assignments). This HCM
// readiness controller keeps its own route "HumanCapital/PositionAssignments"
// and is served by the recovered HumanCapitalService (/api/position-assignments).
[Route("HumanCapital/PositionAssignments")]
public sealed class HumanCapitalPositionAssignmentsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/PositionAssignments/Index.cshtml");
}
