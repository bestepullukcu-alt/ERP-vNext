using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class AgreementController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult _CreateAgreement()
        {
            return View();
        }
    }
}
