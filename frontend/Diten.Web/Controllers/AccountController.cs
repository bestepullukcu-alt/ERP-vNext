using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult ResetPassword()
        {
            return View();
        }

        public IActionResult login()
        {
            return View();
        }

        public IActionResult forgetpassword()
        {
            return View();
        }
    }
}
