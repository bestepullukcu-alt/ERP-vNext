using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
{
    public class CompanyController : Controller
    {
        public IActionResult Company()
        {
            return View();
        }
    }
}
