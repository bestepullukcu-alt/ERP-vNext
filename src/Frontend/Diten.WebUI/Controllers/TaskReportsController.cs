using Microsoft.AspNetCore.Mvc;

namespace Diten.WebUI.Controllers;

public class TaskReportsController : Controller
{
    [HttpGet]
    public IActionResult TaskReport()
    {
        return View();
    }
}
