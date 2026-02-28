using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers;

public class CountryController : Controller
{
    public IActionResult Country()
    {
        return View();
    }
}
