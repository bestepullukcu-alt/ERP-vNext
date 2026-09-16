using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/ConsentVisibilityPolicies")]
public sealed class ConsentVisibilityPoliciesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/TalentEcosystem/ConsentVisibilityPolicies/Index.cshtml");
}
