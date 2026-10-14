using Diten.Web.Security;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0188 tenant workspace. Accepted history is proxied through Gateway;
// PlanningService revalidates the selected company for each read.
[Authorize]
[Route("SupplyChain/DemandPlanning")]
public sealed partial class DemandPlanningController(
    IWebHostEnvironment environment, HttpClient httpClient, IConfiguration configuration) : Controller
{
    private readonly string _gatewayUrl = (configuration["GatewayUrl"] ??
        throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? preview = null)
    {
        ViewData["CanCreateRun"] = PermissionClaims.HasPermission(User, "demand.forecast-runs.create");
        ViewData["CanCreateDraft"] = PermissionClaims.HasPermission(User, "demand.drafts.create");
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

    [HttpGet("history")]
    public IActionResult History()
    {
        ViewData["CanReadHistory"] = PermissionClaims.HasPermission(User, "demand.history.read");
        return View("~/Views/SupplyChain/DemandPlanning/History.cshtml");
    }

    [HttpGet("api/history/accepted-rows")]
    public async Task<IActionResult> AcceptedRows([FromQuery] Guid legalEntityId,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionClaims.HasPermission(User, "demand.history.read")) return Forbid();
        if (legalEntityId == Guid.Empty || skip < 0 || take is < 1 or > 100) return BadRequest();
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_gatewayUrl}/api/v2/demand/history-import-batches/accepted-rows?skip={skip}&take={take}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", legalEntityId.ToString("D"));
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json",
                Content = await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }
}
