using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("HumanCapital/PositionAssignments")]
public sealed class PositionAssignmentsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/PositionAssignments/Index.cshtml");
}
