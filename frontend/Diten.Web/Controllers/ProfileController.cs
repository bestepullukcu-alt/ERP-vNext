using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// WP-EMAIL-LANG-01 delivery 2 — My profile (<c>/Profile</c>): the signed-in tenant user's own language, which AuthService
/// keeps as <c>PreferredLanguage</c> and every mail to them is written in (CT decision 1 = A: one field).
///
/// <para><b>Two same-origin proxies, nothing else.</b> <c>GET /Profile/api/me</c> → the gateway's <c>GET api/auth/me</c>, and
/// <c>PUT /Profile/api/preferences</c> → <c>PUT api/auth/me/preferences</c>. The token and tenant are attached here from the
/// HTTP-only cookie; the browser never sees either. Upstream status and body pass through verbatim, so the page can say
/// a refusal by its code (<c>USER_PREFERRED_LANGUAGE_INVALID</c>) in seven languages.</para>
///
/// <para><b>Only the language travels.</b> The write re-builds its body from <c>preferredLanguage</c> alone: whatever else
/// the page (or anyone) posts never reaches AuthService — which refuses it again on its own side.</para>
///
/// <para><b>The UI follows the choice.</b> A saved language also becomes the culture cookie (<c>.AspNetCore.Culture</c>),
/// the same cookie the top bar's language menu writes; clearing the choice leaves the cookie as it is. The top bar's
/// menu stays a per-browser choice and never changes the mail language.</para>
///
/// <para>No permission key: every tenant user has a profile. The shell's own filter answers an anonymous caller with the
/// login page and a non-tenant actor with 403 before this controller runs.</para>
/// </summary>
[Authorize]
public sealed class ProfileController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    /// <summary>The seven tenant languages — the only values the culture cookie is ever written with from here.</summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ProfileController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("/Profile")]
    public IActionResult Index()
    {
        ViewBag.ActiveMenu = "profile";
        return View("~/Views/Profile/Index.cshtml");
    }

    [HttpGet("/Profile/api/me")]
    public Task<IActionResult> Me() => ForwardAsync(HttpMethod.Get, $"{_gatewayUrl}/api/auth/me", null, writeCulture: false);

    [HttpPut("/Profile/api/preferences")]
    public async Task<IActionResult> SavePreferences()
    {
        if (!TryReadLanguage(await ReadBodyAsync(), out var language))
        {
            return BadRequest(new { message = "preferredLanguage must be a string or null." });
        }

        var body = JsonSerializer.Serialize(new { preferredLanguage = language });
        return await ForwardAsync(HttpMethod.Put, $"{_gatewayUrl}/api/auth/me/preferences", body, writeCulture: true);
    }

    /// <summary>The body's <c>preferredLanguage</c> (string or null) and nothing else; anything that is not a JSON object
    /// with that shape is refused before anything is sent.</summary>
    internal static bool TryReadLanguage(string? json, out string? language)
    {
        language = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty("preferredLanguage", out var value))
            {
                return true; // absent = no choice (null): the same as clearing
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                    return true;
                case JsonValueKind.String:
                    language = value.GetString();
                    return true;
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The language AuthService saved, from its answer — written to the culture cookie only when it is one of the
    /// seven (null when the choice was cleared, or the answer cannot be read).</summary>
    internal static string? SavedLanguage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("preferredLanguage", out var value)
                && value.ValueKind == JsonValueKind.String
                && Languages.Contains(value.GetString()))
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private async Task<string?> ReadBodyAsync()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(HttpContext.RequestAborted);
    }

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string target, string? jsonBody, bool writeCulture)
    {
        if (!TryCreateTenantRequest(method, target, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (jsonBody is not null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                if (writeCulture && response.IsSuccessStatusCode && SavedLanguage(content) is { } saved)
                {
                    Response.Cookies.Append(
                        CookieRequestCultureProvider.DefaultCookieName,
                        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(saved)),
                        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), Path = "/" });
                }

                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Profile proxy could not reach {Method} {TargetUrl}.", method, target);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Profile dependency unavailable." });
        }
    }

    private bool TryCreateTenantRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !TryResolveTenantId(token, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName,
            Request.Headers.TryGetValue(CorrelationHeaderName, out var correlation) && !string.IsNullOrWhiteSpace(correlation.ToString())
                ? correlation.ToString()
                : HttpContext.TraceIdentifier);
        return true;
    }

    private static bool TryResolveTenantId(string token, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claim = jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, "tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
            return Guid.TryParse(claim, out tenantId) && tenantId != Guid.Empty && jwt.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }
}
