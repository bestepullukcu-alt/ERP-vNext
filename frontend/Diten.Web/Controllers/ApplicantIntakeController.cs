using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("HumanCapital/ApplicantIntake")]
public sealed class ApplicantIntakeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/ApplicantIntake/Index.cshtml");
}
