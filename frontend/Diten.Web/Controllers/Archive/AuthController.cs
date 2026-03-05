using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
{
    public class AuthController : Controller
    {
        public IActionResult OAuthSuccess()
        {
            return View();
        }
    }
}
