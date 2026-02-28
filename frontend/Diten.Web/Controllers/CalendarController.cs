using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class CalendarController : Controller
    {

        [Route("calendar/my-calendar")]
        public IActionResult Index()
        {
            return View();
        }

        [Route("calendar/timer-popup")]
        public IActionResult TimerPopup(string taskId,string start)
        {
            return View();
        }
    }
}
