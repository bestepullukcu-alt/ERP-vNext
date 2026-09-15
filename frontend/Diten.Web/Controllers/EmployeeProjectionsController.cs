using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("HumanCapital/EmployeeProjections")]
public sealed class EmployeeProjectionsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/EmployeeProjections/Index.cshtml");
}
