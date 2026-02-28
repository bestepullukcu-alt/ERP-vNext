using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class SafetyReportController : Controller
    {
        [Route("pv-system/safety-report")]
        public IActionResult SafetyReport()
        {
            return View();
        }

        [Route("pv-system/create-safety-report")]
        public IActionResult AddSafetyReport()
        {
            return View();
        }
    }
}
