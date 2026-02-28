using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers
{
    [Route("[controller]")]
    public class ThemeSandboxController : Controller
    {
        [HttpGet("UserList")]
        public IActionResult UserList()
        {
            return View();
        }

        [HttpGet("ProductAdd")]
        public IActionResult ProductAdd()
        {
            return View();
        }

        [HttpGet("Analytics")]
        public IActionResult Analytics()
        {
            return View();
        }
    }
}
