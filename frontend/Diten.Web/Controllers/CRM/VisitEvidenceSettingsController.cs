using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-d) — the visit evidence settings page: the tenant's "Start the visit" rules (form + preview + save
/// confirm), a rep's effective rules, the change history and the time-boxed exceptions approved in the Work Center. All
/// business traffic is proxied server-side through the Gateway; the browser never sees a service URL or a bearer token,
/// and the CrmService stays the authoritative permission + validation layer.
/// <para>Every proxy requires the keys of its CRM endpoint: the policy reads (policy, history, effective, reps)
/// <c>crm.visit-evidence-policy.read</c> OR <c>.manage</c>; the policy PUT <c>.manage</c>; the exception reads and the
/// reviewer's decision <c>crm.visit-evidence-exception.read</c>; every other exception write <c>.manage</c>.</para>
/// </summary>
[Authorize]
[Route("CRM/VisitEvidenceSettings")]
public sealed class VisitEvidenceSettingsController : Controller
{
    public const string PolicyRead = "crm.visit-evidence-policy.read";
    public const string PolicyManage = "crm.visit-evidence-policy.manage";
    public const string ExceptionRead = "crm.visit-evidence-exception.read";
    public const string ExceptionManage = "crm.visit-evidence-exception.manage";

    private const string ViewRoot = "~/Views/CRM/VisitEvidenceSettings";
    private const string Policy = "/api/crm/visit-evidence-policy";
    private const string Exceptions = "/api/crm/visit-evidence-exceptions";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<VisitEvidenceSettingsController> _logger;

    public VisitEvidenceSettingsController(
        HttpClient httpClient, IConfiguration configuration, ILogger<VisitEvidenceSettingsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- page ----------------

    /// <summary>The page. Without a policy read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index()
        => View($"{ViewRoot}/Index.cshtml", new VisitEvidenceSettingsIndexViewModel
        {
            CanRead = HasAny(PolicyRead, PolicyManage),
            CanManagePolicy = HasAny(PolicyManage),
            CanReadExceptions = HasAny(ExceptionRead),
            CanManageExceptions = HasAny(ExceptionManage),
            CanReadQrCodes = HasAny("crm.visit-qr.read", "crm.visit-qr.manage"), // W3E — the "manage institution QR codes" link
            Me = KnowledgePathStudioReview.ActorOf(User)
        });

    // ---------------- the tenant policy ----------------

    [HttpGet("api/policy")]
    public Task<IActionResult> GetPolicy(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, Policy, null, ct, PolicyRead, PolicyManage);

    [HttpPut("api/policy")]
    public Task<IActionResult> PutPolicy(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Put, Policy, ct, PolicyManage);

    [HttpGet("api/policy/history")]
    public Task<IActionResult> History(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Policy}/history{Request.QueryString}", null, ct, PolicyRead, PolicyManage);

    [HttpGet("api/policy/effective")]
    public Task<IActionResult> Effective(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Policy}/effective{Request.QueryString}", null, ct, PolicyRead, PolicyManage);

    [HttpGet("api/policy/reps")]
    public Task<IActionResult> Reps(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Policy}/reps{Request.QueryString}", null, ct, PolicyRead, PolicyManage);

    // ---------------- the time-boxed exceptions ----------------

    [HttpGet("api/exceptions")]
    public Task<IActionResult> ListExceptions(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Exceptions}{Request.QueryString}", null, ct, ExceptionRead);

    [HttpGet("api/exceptions/{id:guid}")]
    public Task<IActionResult> GetException(Guid id, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Exceptions}/{id}", null, ct, ExceptionRead);

    [HttpPost("api/exceptions")]
    public Task<IActionResult> CreateException(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, Exceptions, ct, ExceptionManage);

    [HttpPut("api/exceptions/{id:guid}")]
    public Task<IActionResult> UpdateException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Put, $"{Exceptions}/{id}", ct, ExceptionManage);

    [HttpPost("api/exceptions/{id:guid}/submit")]
    public Task<IActionResult> SubmitException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/submit", ct, ExceptionManage);

    [HttpPost("api/exceptions/{id:guid}/withdraw")]
    public Task<IActionResult> WithdrawException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/withdraw", ct, ExceptionManage);

    /// <summary>The reviewer's decision: the read key (the Work Center task decides who may — the CRM rule).</summary>
    [HttpPost("api/exceptions/{id:guid}/decision")]
    public Task<IActionResult> DecideException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/decision", ct, ExceptionRead);

    [HttpPost("api/exceptions/{id:guid}/cancel")]
    public Task<IActionResult> CancelException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/cancel", ct, ExceptionManage);

    [HttpPost("api/exceptions/{id:guid}/end-early")]
    public Task<IActionResult> EndExceptionEarly(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/end-early", ct, ExceptionManage);

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyBodyAsync(HttpMethod method, string path, CancellationToken ct, params string[] anyOf)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(method, path, body ?? "{}", ct, anyOf);
    }

    /// <summary>The caller needs ANY of <paramref name="anyOf"/> (one key for a write; read OR manage for a policy read).</summary>
    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] anyOf)
    {
        if (!HasAny(anyOf))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, rawBody, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visit evidence settings Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        // A bodiless upstream status (204/304/…) must not be turned into a body — the same-origin-proxy 204→500 trap.
        if (IsBodilessStatus(response.StatusCode))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        if (CrmVisitExecutionController.ProblemDetailsToEnvelope(
                response.Content.Headers.ContentType?.MediaType, content, (int)response.StatusCode) is { } envelope)
        {
            return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = envelope };
        }

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private static bool IsBodilessStatus(HttpStatusCode status)
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent
            or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    private static bool ContainsTenantId(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAny(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));
}
