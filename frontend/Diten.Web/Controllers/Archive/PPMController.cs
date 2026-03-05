using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Archive
{
    public class PPMController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }


        [Route("ppm/workflow-overview")]
        public IActionResult Workflow()
        {
            return View("~/Views/PPM/Workflow/Workflow.cshtml");
        }

        [Route("ppm/{id}/new-record")]
        public IActionResult NewRecord(string id, int recordTypeId,string recordType,string categoryId,string category)
        {
            return View("~/Views/PPM/Workflow/NewRecord.cshtml");
        }

        [Route("ppm/my-workflow")]
        public IActionResult MyWorkFlow()
        {
            return View("~/Views/PPM/MyWorkFlow/MyWorkFlow.cshtml");
        }
    }
}
