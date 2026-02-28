using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class PvSystemController : Controller
    {
        [Route("pv-system/lcppv")]
        public IActionResult Lcppv()
        {
            return View();
        }


        [Route("pv-system/create-lcppv")]
        public IActionResult AddLcppv()
        {
            return View();
        }
    }
}
