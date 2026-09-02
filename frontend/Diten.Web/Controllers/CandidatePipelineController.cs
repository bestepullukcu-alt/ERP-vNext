using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("HumanCapital/CandidatePipeline")]
public sealed class CandidatePipelineController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/HumanCapital/CandidatePipeline/Index.cshtml");
}
