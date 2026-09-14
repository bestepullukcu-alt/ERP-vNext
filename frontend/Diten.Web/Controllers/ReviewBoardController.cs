using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/ReviewBoard")]
public sealed class ReviewBoardController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/TalentEcosystem/ReviewBoard/Index.cshtml");
}
