using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
{
    public class UserManagementController : Controller
    {
        public IActionResult Role()
        {
            return View();
        }

        public IActionResult _AddRole()
        {
            return View();
        }
        public IActionResult _UpdateRole()
        {
            return View();
        }
    }
}
