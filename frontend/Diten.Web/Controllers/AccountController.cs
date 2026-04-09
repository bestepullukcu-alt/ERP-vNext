using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Diten.Web.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        [HttpGet("/account/login")]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true || HasValidToken())
                return Redirect(returnUrl ?? "/Products");
            
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpGet("/account/logout")]
        public IActionResult Logout()
        {
            // Clear cookies
            Response.Cookies.Delete("access_token");
            Response.Cookies.Delete("refresh_token");
            
            return RedirectToAction("Login");
        }

        private bool HasValidToken()
        {
            return Request.Cookies.ContainsKey("access_token");
        }
    }
}
