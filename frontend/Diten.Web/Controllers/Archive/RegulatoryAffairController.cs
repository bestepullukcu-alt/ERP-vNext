using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
{
    public class RegulatoryAffairController : Controller
    {

        [Route("regulatory-affair/regulatory-report")]
        public IActionResult RegulatoryReport()
        {
            return View();
        }
        [Route("regulatory-affair/create-regulatory-report")]
        public IActionResult CreateRegulatoryReport()
        {
            return View();
        }
    }
}
