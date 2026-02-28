using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class UserListController : Controller
    {
        
        [Route("user-management/user")]
        public IActionResult UserList()
        {
            return View();
        }
    }
}
