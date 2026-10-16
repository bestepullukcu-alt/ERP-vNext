using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

public sealed partial class DemandPlanningController
{
    [HttpGet("api/forecast-runs/evaluation")]
    public Task<IActionResult> EvaluateForecast([FromQuery] Guid legalEntityId,
        [FromQuery] Guid planningCycleId, [FromQuery] Guid skuId,
        [FromQuery] string warehouseId, CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, "demand.plans.read") ||
            !PermissionClaims.HasPermission(User, "demand.forecast-runs.create"))
            return Task.FromResult<IActionResult>(Forbid());
        if (planningCycleId == Guid.Empty || skuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(warehouseId) || warehouseId.Length > 200)
            return Task.FromResult<IActionResult>(BadRequest());
        var path = $"/api/v2/demand/forecast-runs/evaluation?planningCycleId={planningCycleId:D}" +
            $"&skuId={skuId:D}&warehouseId={Uri.EscapeDataString(warehouseId)}";
        return ForecastGatewayAsync(HttpMethod.Get, path, legalEntityId, null, cancellationToken);
    }

    [HttpPost("api/forecast-runs")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateForecast([FromQuery] Guid legalEntityId,
        [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, "demand.plans.read") ||
            !PermissionClaims.HasPermission(User, "demand.forecast-runs.create") ||
            !PermissionClaims.HasPermission(User, "demand.drafts.create"))
            return Task.FromResult<IActionResult>(Forbid());
        if (body.ValueKind != JsonValueKind.Object)
            return Task.FromResult<IActionResult>(BadRequest());
        return ForecastGatewayAsync(HttpMethod.Post, "/api/v2/demand/forecast-runs",
            legalEntityId, body.GetRawText(), cancellationToken);
    }

    [HttpGet("api/revisions/{revisionId:guid}")]
    public Task<IActionResult> ForecastDraft(Guid revisionId,
        [FromQuery] Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, "demand.plans.read"))
            return Task.FromResult<IActionResult>(Forbid());
        return ForecastGatewayAsync(HttpMethod.Get,
            $"/api/v2/demand/revisions/{revisionId:D}", legalEntityId, null, cancellationToken);
    }

    private async Task<IActionResult> ForecastGatewayAsync(HttpMethod method,
        string path, Guid legalEntityId, string? body, CancellationToken cancellationToken)
    {
        if (legalEntityId == Guid.Empty) return BadRequest();
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        // Scope comes from the authenticated server principal, never browser headers.
        var tenantClaim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId");
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty)
            return Forbid();
        using var request = new HttpRequestMessage(method, _gatewayUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        request.Headers.Add("X-Legal-Entity-Id", legalEntityId.ToString("D"));
        var trace = Request.Headers["X-Correlation-Id"];
        if (trace.Count == 1 && Guid.TryParse(trace[0], out var correlation))
            request.Headers.Add("X-Correlation-Id", correlation.ToString("D"));
        if (method == HttpMethod.Post)
        {
            var key = Request.Headers["Idempotency-Key"];
            if (key.Count != 1 || string.IsNullOrWhiteSpace(key[0]) || key[0]!.Length > 200)
                return BadRequest();
            request.Headers.Add("Idempotency-Key", key[0]);
            request.Content = new StringContent(body!, Encoding.UTF8, "application/json");
        }
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode, ContentType = "application/json",
                Content = await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }
}