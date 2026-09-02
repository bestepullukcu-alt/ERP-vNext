using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/PayrollIntegrationGovernance")]
public sealed class PayrollIntegrationGovernanceController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/PayrollIntegrationGovernance/Index.cshtml");
}
