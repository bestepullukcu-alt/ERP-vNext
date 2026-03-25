using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

public class WorkCenterController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
