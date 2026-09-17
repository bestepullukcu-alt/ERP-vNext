using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/ReferenceExchange")]
public sealed class ReferenceExchangeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/TalentEcosystem/ReferenceExchange/Index.cshtml");
}
