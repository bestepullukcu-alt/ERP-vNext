using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class PharmaceuticalFormController : Controller
    {
        public IActionResult PharmaceuticalForm()
        {
            return View();
        }
    }
}
