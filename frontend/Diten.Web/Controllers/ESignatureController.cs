using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
//
// MOD-0022 E5W2 — the six pages of pack §11.1 (UAS-001 in each view) and three read helpers, all on contract operations:
//   • the export file (PDF / CSV, §4.25) comes back as BYTES with its Content-Disposition and X-Export-Number, no-store
//     (CT-D20) — the text path would corrupt a PDF;
//   • list/requests and list/signatures translate the list component's server wire (start / length → page / pageSize, the
//     answer → { items, total, filteredTotal }) for listSignatureRequests (§4.10) and searchSignatures (§4.24) — the same
//     operations, no other parameter;
//   • companies → MDM GET /api/legal-entities/lookup (the tenant's companies for the policy scope and the export filter; the
//     E2 policy handler answers only the companies that already have an override — "MDM owns the full list").
[Authorize]
[Route("ESignature")]
public sealed class ESignatureController : Controller
{
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string ExportNumberHeader = "X-Export-Number";
    private const int MaxPageSize = 100; // ESIGN-BUNDLE PageSize maximum

    private static readonly string[] FileTypes = ["application/pdf", "text/csv", "application/octet-stream"];

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

    /// <summary>İmza talepleri (ESIGN_REQUESTS, esign.requests.read).</summary>
    [HttpGet("Requests")]
    public IActionResult Requests() => View("~/Views/ESignature/Requests.cshtml");

    /// <summary>Doğrulama (ESIGN_VERIFICATION, esign.signatures.read) — ?module=&amp;type=&amp;id= or ?signature=.</summary>
    [HttpGet("Verification")]
    public IActionResult Verification() => View("~/Views/ESignature/Verification.cshtml");

    /// <summary>Denetim dışa aktarımı (ESIGN_AUDITEXPORT, esign.signatures.export).</summary>
    [HttpGet("AuditExport")]
    public IActionResult AuditExport() => View("~/Views/ESignature/AuditExport.cshtml");

    /// <summary>e-İmza politikası (ESIGN_POLICY, esign.policy.read).</summary>
    [HttpGet("Settings/Policy")]
    public IActionResult Policy() => View("~/Views/ESignature/Settings/Policy.cshtml");

    /// <summary>İmza anlamları (ESIGN_MEANINGS, esign.meanings.read).</summary>
    [HttpGet("Settings/Meanings")]
    public IActionResult Meanings() => View("~/Views/ESignature/Settings/Meanings.cshtml");

    /// <summary>İmza örneği onayları (ESIGN_SAMPLEAPPROVALS, esign.samples.approve).</summary>
    [HttpGet("Settings/SampleApprovals")]
    public IActionResult SampleApprovals() => View("~/Views/ESignature/Settings/SampleApprovals.cshtml");

    [HttpPost("auth/reauth")]
    public Task<IActionResult> Reauth() => ProxyGatewayAsync("/api/tenant-auth/esign/reauth");

    [HttpPost("auth/ticket")]
    public Task<IActionResult> WindowTicket() => ProxyGatewayAsync("/api/tenant-auth/esign/ticket");

    [HttpGet("api/{**everything}")]
    [HttpPost("api/{**everything}")]
    [HttpPut("api/{**everything}")]
    public Task<IActionResult> ProxyApi(string? everything)
        => Diten.Web.Services.Proxy.ProxyPath.TryBuild("/api/v1/esign", everything, out var path, out var refused) // WP-MVP1-SEC-PROXY1: the prefix boundary
            ? ProxyGatewayAsync(path)
            : Task.FromResult(Diten.Web.Services.Proxy.ProxyPath.Refusal(refused));

    /// <summary>The Requests list (server mode) → listSignatureRequests (scope mine) with the page's module / status / dates.</summary>
    [HttpGet("list/requests")]
    public Task<IActionResult> RequestList()
        => ListAsync("/api/v1/esign/requests", ["module", "status", "fromUtc", "toUtc"], ("scope", "mine"));

    /// <summary>The export table (server mode) → searchSignatures with the export filter.</summary>
    [HttpGet("list/signatures")]
    public Task<IActionResult> SignatureList()
        => ListAsync("/api/v1/esign/signatures", ["fromUtc", "toUtc", "module", "signerUserId", "meaningCode", "legalEntityId"]);

    /// <summary>The tenant's referenceable companies [{ id, name }] (MDM lookup); [] when MDM cannot be read.</summary>
    [HttpGet("companies")]
    public async Task<IActionResult> Companies()
    {
        var reply = await SendAsync(HttpMethod.Get, "/api/legal-entities/lookup", string.Empty, null);
        if (reply is null)
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        var companies = reply.Ok ? Diten.Web.Models.Inventory.CompanySelection.ParseCompanies(Encoding.UTF8.GetString(reply.Body)) : null;
        Response.Headers.CacheControl = "no-store";
        return Json(new { data = (companies ?? []).Select(c => new { id = c.Id, name = c.Name }) });
    }

    private async Task<IActionResult> ListAsync(string targetPath, string[] filterKeys, params (string Key, string Value)[] fixedParameters)
    {
        var query = Request.Query;
        var length = int.TryParse(query["length"], out var l) && l > 0 ? Math.Min(l, MaxPageSize) : 25;
        var start = int.TryParse(query["start"], out var s) && s > 0 ? s : 0;
        var page = (start / length) + 1;

        var parts = new List<string>();
        foreach (var (key, value) in fixedParameters)
        {
            parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }

        foreach (var key in filterKeys)
        {
            var value = query[key].ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // A date filter (yyyy-MM-dd, the page's date input) covers the whole UTC day: from 00:00, to 23:59:59.999.
            if (key is "fromUtc" or "toUtc" && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                value = key == "fromUtc" ? $"{value}T00:00:00Z" : $"{value}T23:59:59.999Z";
            }

            parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }

        parts.Add($"page={page.ToString(CultureInfo.InvariantCulture)}");
        parts.Add($"pageSize={length.ToString(CultureInfo.InvariantCulture)}");

        var reply = await SendAsync(HttpMethod.Get, targetPath, "?" + string.Join('&', parts), null);
        if (reply is null)
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        if (!reply.Ok)
        {
            return new ContentResult { StatusCode = reply.Status, Content = Encoding.UTF8.GetString(reply.Body), ContentType = reply.ContentType };
        }

        // The contract page { items, total, page, pageSize[, waitingCount] } → the list wire { items, total, filteredTotal }.
        var envelope = JsonNode.Parse(reply.Body) as JsonObject;
        var data = envelope?["data"] as JsonObject ?? new JsonObject();
        var total = data["total"]?.GetValue<int>() ?? 0;
        data["filteredTotal"] = total;
        Response.Headers.CacheControl = "no-store";
        return new ContentResult { StatusCode = 200, Content = new JsonObject { ["data"] = data.DeepClone() }.ToJsonString(), ContentType = "application/json" };
    }

    private async Task<IActionResult> ProxyGatewayAsync(string targetPath)
    {
        string? body = null;
        if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method) && Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        var reply = await SendAsync(new HttpMethod(Request.Method), targetPath, Request.QueryString.ToString(), body);
        if (reply is null)
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        if (reply.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "e-Signature gateway proxy unavailable." });
        }

        if (reply.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers.CacheControl = "no-store";
            return new FileContentResult(reply.Body, reply.ContentType) { };
        }

        // E5W2 (CT-D20) — the export file: bytes, its file name and its export number, never cached.
        if (reply.Ok && FileTypes.Any(type => reply.ContentType.StartsWith(type, StringComparison.OrdinalIgnoreCase)))
        {
            Response.Headers.CacheControl = "no-store";
            if (reply.ExportNumber is not null)
            {
                Response.Headers[ExportNumberHeader] = reply.ExportNumber;
            }

            return reply.FileName is null
                ? new FileContentResult(reply.Body, reply.ContentType)
                : new FileContentResult(reply.Body, reply.ContentType) { FileDownloadName = reply.FileName };
        }

        return new ContentResult
        {
            StatusCode = reply.Status,
            Content = Encoding.UTF8.GetString(reply.Body),
            ContentType = reply.ContentType
        };
    }

    private sealed record GatewayReply(int Status, bool Ok, string ContentType, byte[] Body, string? FileName, string? ExportNumber, bool Unavailable);

    /// <summary>One gateway call with the caller's token; null when there is no token or no tenant. The body is never logged.</summary>
    private async Task<GatewayReply?> SendAsync(HttpMethod method, string targetPath, string queryString, string? body)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tenantId = ResolveTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return new GatewayReply(400, false, "application/json",
                JsonSerializer.SerializeToUtf8Bytes(new { message = $"'{TenantHeaderName}' or JWT tenant_id claim is required." }), null, null, false);
        }

        var targetUrl = $"{_gatewayUrl}{targetPath}{queryString}";
        var correlationId = ResolveCorrelationId();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var proxyRequest = new HttpRequestMessage(method, targetUrl);
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

            if (body is not null)
            {
                proxyRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            var statusCode = (int)response.StatusCode;
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            var bytes = await response.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);
            stopwatch.Stop();
            _logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "e-Signature proxy completed. method={Method} path={Path} status={StatusCode} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                method.Method, targetPath, statusCode, tenantId, correlationId, stopwatch.ElapsedMilliseconds);

            var disposition = response.Content.Headers.ContentDisposition;
            var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');
            var exportNumber = response.Headers.TryGetValues(ExportNumberHeader, out var numbers) ? numbers.FirstOrDefault() : null;
            return new GatewayReply(statusCode, response.IsSuccessStatusCode, contentType, bytes, fileName, exportNumber, false);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "e-Signature proxy failed. method={Method} path={Path} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                method.Method, targetPath, tenantId, correlationId, stopwatch.ElapsedMilliseconds);
            return new GatewayReply(503, false, "application/json", [], null, null, true);
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
