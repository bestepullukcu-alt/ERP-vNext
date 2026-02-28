using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult OAuthSuccess()
        {
            return View();
        }
    }
}
