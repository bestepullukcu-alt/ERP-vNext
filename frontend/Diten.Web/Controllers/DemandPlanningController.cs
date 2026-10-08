using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0188 tenant workspace. A live Gateway route and authoritative company-list
// contract have not been accepted, so this controller exposes no data proxy.
[Authorize]
[Route("SupplyChain/DemandPlanning")]
public sealed class DemandPlanningController(IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery] string? preview = null)
    {
        ViewData["CanRead"] = PermissionClaims.HasPermission(User, "demand.plans.read");
        ViewData["CanEdit"] = PermissionClaims.HasPermission(User, "demand.drafts.update");
        ViewData["CanReview"] = PermissionClaims.HasPermission(User, "demand.plans.review");
        ViewData["CanConsume"] = PermissionClaims.HasPermission(User, "demand.plans.consume");
        ViewData["CanAudit"] = PermissionClaims.HasPermission(User, "demand.audit.read");
        // An explicit development-only fixture is never an authority for production
        // company scope, plan data, or a successful backend operation.
        ViewData["FixturePreview"] = environment.IsDevelopment() &&
            string.Equals(preview, "fixture", StringComparison.Ordinal);
        return View("~/Views/SupplyChain/DemandPlanning/Index.cshtml");
    }
}
