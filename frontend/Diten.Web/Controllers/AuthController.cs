using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult OAuthSuccess()
        {
            return View();
        }
    }
}
