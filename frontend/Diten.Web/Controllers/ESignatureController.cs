using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0022 E5W1 — e-İmza tenant pages + the same-origin proxy of the signature dialog (pack §11.2, KS-0).
// Tenant shell only: the route is /ESignature/**, never /Platform (KS-0). The controller holds no e-signature logic:
//   • api/{**everything}  → Gateway /api/v1/esign/{everything}        (Platform, ESIGN-BUNDLE §4.4–4.26)
//   • auth/reauth         → Gateway /api/tenant-auth/esign/reauth     (AuthService, ESIGN-BUNDLE §4.1 — password → ticket)
//   • auth/ticket         → Gateway /api/tenant-auth/esign/ticket     (AuthService, ESIGN-BUNDLE §4.2 — window ticket, CT-D7)
// The proxy is the WorkflowController pattern (WorkflowController.cs:133-230): the caller's own token from the auth cookie,
// TenantId from the token (never from the browser), correlation id passed through. Bodies are forwarded as they are and are
// NEVER logged — they carry the password (reauth) and the single-use ticket (sign / sample); the log line holds method, path,
// status and timing only (ESIGN-BUNDLE x-rules 8). Binary answers (sample image PNG) are returned as bytes.
// Every endpoint keeps its own server-side key check ([HasPermission] in Platform); the page check here is UAS-001 display only.
[Authorize]
[Route("ESignature")]
public sealed class ESignatureController : Controller
{
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<ESignatureController> _logger;

    public ESignatureController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ESignatureController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    /// <summary>İmzam (ESIGN_MYSIGNATURE, esign.my-samples.manage) — UAS-001 in the view.</summary>
    [HttpGet("MySignature")]
    public IActionResult MySignature() => View("~/Views/ESignature/MySignature.cshtml");

    [HttpPost("auth/reauth")]
    public Task<IActionResult> Reauth() => ProxyGatewayAsync("/api/tenant-auth/esign/reauth");

    [HttpPost("auth/ticket")]
    public Task<IActionResult> WindowTicket() => ProxyGatewayAsync("/api/tenant-auth/esign/ticket");

    [HttpGet("api/{**everything}")]
    [HttpPost("api/{**everything}")]
    [HttpPut("api/{**everything}")]
    public Task<IActionResult> ProxyApi(string? everything)
        => ProxyGatewayAsync(string.IsNullOrWhiteSpace(everything) ? "/api/v1/esign" : $"/api/v1/esign/{everything.TrimStart('/')}");

    private async Task<IActionResult> ProxyGatewayAsync(string targetPath)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        var tenantId = ResolveTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = $"'{TenantHeaderName}' or JWT tenant_id claim is required." });
        }

        var targetUrl = $"{_gatewayUrl}{targetPath}{Request.QueryString}";
        var correlationId = ResolveCorrelationId();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var proxyRequest = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);
            proxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            proxyRequest.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
            proxyRequest.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId);
            if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
            }

            if (Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.ToString());
            }

            if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method) && Request.ContentLength is > 0)
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                proxyRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            var statusCode = (int)response.StatusCode;
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            stopwatch.Stop();
            _logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "e-Signature proxy completed. method={Method} path={Path} status={StatusCode} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                Request.Method, targetPath, statusCode, tenantId, correlationId, stopwatch.ElapsedMilliseconds);

            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);
                Response.Headers.CacheControl = "no-store";
                return new FileContentResult(bytes, contentType) { };
            }

            return new ContentResult
            {
                StatusCode = statusCode,
                Content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted),
                ContentType = contentType
            };
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "e-Signature proxy failed. method={Method} path={Path} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                Request.Method, targetPath, tenantId, correlationId, stopwatch.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "e-Signature gateway proxy unavailable." });
        }
    }

    private string ResolveCorrelationId()
        => Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) && !string.IsNullOrWhiteSpace(correlationId.ToString())
            ? correlationId.ToString()
            : HttpContext.TraceIdentifier;

    private string? ResolveTenantId(string token)
    {
        if (Guid.TryParse(FindTenantClaim(User.Claims), out var claimTenantId))
        {
            return claimTenantId.ToString();
        }

        try
        {
            return Guid.TryParse(FindTenantClaim(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims), out var jwtTenantId)
                ? jwtTenantId.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindTenantClaim(IEnumerable<Claim> claims)
        => claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
