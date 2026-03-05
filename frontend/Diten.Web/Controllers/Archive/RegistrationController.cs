using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
{
    public class RegistrationController : Controller
    {
        [Route("registration/marketing-authorization")]

        public IActionResult Registration()
        {
            return View();
        }


        [Route("registration/add-marketing-authorization")]
        public IActionResult AddRegistration()
        {
            return View();
        }

        [Route("registration/edit-marketing-authorization")]
        public IActionResult EditRegistration()
        {
            return View();
        }
    }
}
