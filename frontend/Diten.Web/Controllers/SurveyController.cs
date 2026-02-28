using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class SurveyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }


        [Route("survey/survey-type")]
        public IActionResult SurveyType()
        {
            return View("~/Views/Survey/SurveyType/SurveyType.cshtml");
        }

        [Route("survey/survey-list")]
        public IActionResult SurveyList()
        {
            return View("~/Views/Survey/SurveyList/SurveyList.cshtml");
        }


        [Route("survey/{id}/manage-template")]
        public IActionResult ManageSurveyTemplate(string id)
        {
            return View("~/Views/Survey/SurveyList/ManageSurveyTemplate.cshtml");
        }

        [Route("survey/{id}/manage")]
        public IActionResult Manage(string id, string templateId)
        {
            // templateId doluysa design sayfasına yönlendir
            //if (!string.IsNullOrEmpty(templateId))
            //{
            //    if (templateId == "blank")
            //    {
            //        // Blank design için ayrı sayfa
            //        return View("~/Views/Survey/Design/Blank.cshtml");
            //    }

            //    // Template'e göre design sayfası
            //    return View("~/Views/Survey/Design/Design.cshtml");
            //}

            // Template seçimi ekranı (ilk giriş)
            return View("~/Views/Survey/SurveyList/Manage.cshtml");
        }

        [Route("survey/{id}/preview")]
        public IActionResult Preview(string id)
        {
            return View("~/Views/Survey/SurveyList/Preview.cshtml");
        }

        [Route("survey/schedule")]
        public IActionResult SurveySchedule()
        {
            return View("~/Views/Survey/SurveySchedule/SurveySchedule.cshtml");
        }

        [Route("survey/my-surveys")]
        public IActionResult MySurvey()
        {
            return View("~/Views/Survey/MySurvey/MySurvey.cshtml");
        }
    }
}
