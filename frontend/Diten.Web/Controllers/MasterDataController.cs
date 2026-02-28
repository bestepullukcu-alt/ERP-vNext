using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class MasterDataController : Controller
    {

        [Route("master-data/authority")]
        public IActionResult Authority()
        {
            return View();
        }

        [Route("master-data/global-sku")]
        public IActionResult GlobalSku()
        {
            return View();
        }

        [Route("master-data/add-globalsku")]
        public IActionResult AddGlobalSku()
        {
            return View();
        }
    }
}
