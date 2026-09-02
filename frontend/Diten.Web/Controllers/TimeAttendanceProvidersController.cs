using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/TimeAttendanceProviders")]
public sealed class TimeAttendanceProvidersController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/TimeAttendanceProviders/Index.cshtml");
}
