using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/TrustLevels")]
public sealed class TrustLevelsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/TalentEcosystem/TrustLevels/Index.cshtml");
}
