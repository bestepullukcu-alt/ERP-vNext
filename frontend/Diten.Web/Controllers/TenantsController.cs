using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/[controller]")]
public class TenantsController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/Platform/Tenants/Index.cshtml");
    }
}
