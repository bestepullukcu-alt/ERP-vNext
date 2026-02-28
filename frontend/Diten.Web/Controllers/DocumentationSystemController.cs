using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.WebUI.Controllers
{
    public class DocumentationSystemController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult _UploadDocument()
        {
            return View();
        }
        public IActionResult _FolderDetail()
        {
            return View();
        }
        public IActionResult _EditDocument()
        {
            return View();
        }
    }
}
