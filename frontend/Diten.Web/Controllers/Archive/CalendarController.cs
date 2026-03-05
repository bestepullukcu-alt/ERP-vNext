using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
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
