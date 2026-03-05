using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive;

public class CountryController : Controller
{
    public IActionResult Country()
    {
        return View();
    }
}
