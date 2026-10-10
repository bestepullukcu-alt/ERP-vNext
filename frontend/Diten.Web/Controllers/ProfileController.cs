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

    /// <summary>The user's own record — PROJECTED to the four fields the page shows (FIX2): AuthService's whole UserDto
    /// (roles, tenant, failed sign-ins, a development set-up link) never reaches the browser from here.</summary>
    [HttpGet("/Profile/api/me")]
    public async Task<IActionResult> Me()
    {
        var result = await ForwardAsync(HttpMethod.Get, $"{_gatewayUrl}/api/auth/me", null, writeCulture: false);
        if (result is not ContentResult { StatusCode: >= 200 and < 300 } answer)
        {
            return result;
        }

        return ProjectMe(answer.Content) is { } projected
            ? new ContentResult { Content = projected, ContentType = "application/json", StatusCode = answer.StatusCode }
            : StatusCode(StatusCodes.Status502BadGateway, new { message = "The profile answer could not be read." });
    }

    /// <summary>{ data: { firstName, lastName, email, preferredLanguage }, isSuccessful, statusCode } — nothing else.</summary>
    internal static string? ProjectMe(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? Text(string name) => data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return JsonSerializer.Serialize(new
            {
                data = new
                {
                    firstName = Text("firstName"),
                    lastName = Text("lastName"),
                    email = Text("email"),
                    preferredLanguage = Text("preferredLanguage")
                },
                isSuccessful = true,
                statusCode = 200
            });
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The largest body the write accepts — a language code needs a few dozen bytes.</summary>
    internal const int MaxBodyBytes = 4096;

    [HttpPut("/Profile/api/preferences")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> SavePreferences()
    {
        // FIX2 — JSON only (415 otherwise), and a body that does not NAME preferredLanguage is refused (400): never read as
        // "clear my choice". Clearing is an explicit null, exactly as AuthService itself requires.
        if (!Request.HasJsonContentType())
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { message = "The body must be JSON." });
        }

        if (!TryReadLanguage(await ReadBodyAsync(), out var language))
        {
            return BadRequest(new { message = "The body must name preferredLanguage: a language, or null to clear it." });
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

            // FIX2 — the field must be THERE (any letter case, as AuthService reads it); absent or misspelled is refused.
            var named = document.RootElement.EnumerateObject()
                .Where(property => string.Equals(property.Name, "preferredLanguage", StringComparison.OrdinalIgnoreCase))
                .Select(property => (JsonElement?)property.Value)
                .FirstOrDefault();
            if (named is not { } value)
            {
                return false;
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
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // InvalidOperationException: a string that is not text (a lone surrogate, "\ud800") — refused, never a 500.
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
