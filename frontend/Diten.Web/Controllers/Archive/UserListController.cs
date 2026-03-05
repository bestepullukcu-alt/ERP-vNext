using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
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
