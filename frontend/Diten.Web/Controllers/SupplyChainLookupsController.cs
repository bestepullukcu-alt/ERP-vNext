using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// R-2 (PR #134, SHIPMENT-BUNDLE 3.2.0). Feeds the legal-entity selector on all five SupplyChain pages from
/// MDM's own lookup, through the gateway, following the MVP-1 pattern (TasksController,
/// WorkingCalendarOverridesController, OrganizationUnitsController all proxy the same
/// <c>/api/legal-entities/lookup</c>).
///
/// ONE route for five pages, not five copies. It also cannot live on the five module controllers: their proxy
/// helpers now go through TryCreateGatewayRequest, which REQUIRES a legalEntityId on the query — and this is the
/// call a page makes to find out which legal entities exist. Routing the feed through a scoped helper would make
/// the selector depend on a value only the selector can supply.
///
/// No scope headers are sent and none are needed: MDM derives the tenant from the JWT
/// (LegalEntityRepository composes TenantFilter), so the list is already the caller's tenant's. The caller needs
/// mdm.legal-entities.read, which the default Admin and Viewer templates already grant through the
/// <c>legal-entity</c> module (Q477).
/// </summary>
[Authorize]
[Route("SupplyChain")]
public sealed class SupplyChainLookupsController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainLookupsController> _logger;

    public SupplyChainLookupsController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupplyChainLookupsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    [HttpGet("api/legal-entities")]
    public async Task<IActionResult> LegalEntities(CancellationToken cancellationToken)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return StatusCode(StatusCodes.Status401Unauthorized);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/lookup");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // The browser's trace is forwarded when it sent one; this feed never mints one, so a page that omits it
        // simply gets no correlation rather than a fabricated id (MODULE-RECIPE 2.2).
        if (Request.Headers.TryGetValue(CorrelationHeader, out var correlation)
            && correlation.Count == 1
            && Guid.TryParse(correlation[0], out var trace))
        {
            request.Headers.TryAddWithoutValidation(CorrelationHeader, trace.ToString("D"));
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation(
                "Legal entity lookup answered {StatusCode} for the SupplyChain selector.", (int)response.StatusCode);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json",
                Content = body,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // The selector cannot be populated. Say so as an outage rather than as an empty list: an empty
            // dropdown would read as "this tenant has no legal entities", which is a different claim.
            _logger.LogWarning(exception, "Legal entity lookup is unavailable for the SupplyChain selector.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
