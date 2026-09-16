using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/VerifiedParticipants")]
public sealed class VerifiedParticipantsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/TalentEcosystem/VerifiedParticipants/Index.cshtml");
}
