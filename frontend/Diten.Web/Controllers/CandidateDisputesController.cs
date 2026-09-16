using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/CandidateDisputes")]
public sealed class CandidateDisputesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/TalentEcosystem/CandidateDisputes/Index.cshtml");
}
