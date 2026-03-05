using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
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
