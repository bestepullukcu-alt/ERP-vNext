using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W4 (W4-WEB-b) — the same-origin Gateway proxy of the pharmacy report and the potential thresholds pages (the W3
/// <c>VisitEvidenceSettingsController</c> plumbing, shared by the two W4 pages instead of copied twice): bearer + tenant
/// header from the session, a JSON body never carrying <c>tenantId</c>, the bodiless-status guard, problem details to the
/// envelope, and a byte pass-through for images / multipart.
/// </summary>
public abstract class CrmGatewayProxy : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger _logger;

    protected CrmGatewayProxy(HttpClient httpClient, IConfiguration configuration, ILogger logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    protected bool HasAny(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    protected bool HasAll(params string[] permissions) => permissions.All(x => PermissionClaims.HasPermission(User, x));

    protected IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });

    /// <summary>A JSON proxy: <paramref name="allowed"/> decides; a body naming <c>tenantId</c> is refused.</summary>
    protected async Task<IActionResult> ProxyAsync(HttpMethod method, string path, string? rawBody, bool allowed, CancellationToken ct,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        if (!allowed) return Denied();
        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path,
            rawBody is null ? null : new StringContent(rawBody, Encoding.UTF8, "application/json"), ct, headers);
        return await ToProxyResultAsync(response, ct);
    }

    /// <summary>A binary read (an image): the bytes and the content type as they came; errors as the JSON proxy answers them.</summary>
    protected async Task<IActionResult> ProxyBytesAsync(string path, bool allowed, CancellationToken ct)
    {
        if (!allowed) return Denied();
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode || IsBodilessStatus(response.StatusCode))
        {
            return await ToProxyResultAsync(response, ct);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return File(bytes, response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
    }

    /// <summary>A multipart write: the browser's body BUFFERED (bounded by the action's [RequestSizeLimit]) and sent as bytes
    /// with its boundary — the W3 live-E4 lesson (a streamed body with a pinned length failed live).</summary>
    protected async Task<IActionResult> ProxyMultipartAsync(string path, bool allowed, CancellationToken ct)
    {
        if (!allowed) return Denied();
        var contentType = Request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType)
            || !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || !MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return BadRequest(new { errors = new[] { "multipart/form-data is required.", "visit_photo_invalid" } });
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var content = new ByteArrayContent(buffer.ToArray());
        content.Headers.ContentType = mediaType;
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Post, path, content, ct), ct);
    }

    protected async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    /// <summary>The <c>data</c> of a gateway answer (or the root), or null when it failed.</summary>
    protected async Task<JsonElement?> ReadDataAsync(string path, CancellationToken ct)
    {
        using var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) ? data : root).Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}") { Content = content };
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var tenantId = TenantId();
            if (string.IsNullOrWhiteSpace(tenantId)) return null;
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language)) request.Headers.TryAddWithoutValidation("Accept-Language", language);
            foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(value)) request.Headers.TryAddWithoutValidation(name, value);
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRM Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    protected static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null) return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        if (IsBodilessStatus(response.StatusCode)) return new StatusCodeResult((int)response.StatusCode);
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
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    public static bool ContainsTenantId(string rawBody)
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

    private string? TenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    protected static string? Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
