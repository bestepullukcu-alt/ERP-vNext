using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// FE-C 3/3 (MOD-0018-FU9) — tenant Users CRUD page (golden-reference Slim). Gateway-proxy controller:
// mutations route through here (antiforgery + bearer/tenant forwarding) to AuthService /api/users; the
// datatable list loads client-side from the gateway. UX-only; backend [HasPermission] is authoritative.
//
// WP-INFRA-AUTH-ACCOUNT-KIND-01 — three same-origin proxies under /Users/api/* (lookup, account-assertion,
// account-kind) in the ProxyAsync shape MeetingsController/TasksController already use: the JWT is read
// server-side from the auth cookie, the upstream status passes through verbatim, and the browser never
// addresses a service port. Create additionally forwards an optional AccountKind; AuthService refuses it
// with 403 unless the caller holds auth.users.account-kind.manage (explicit-grant-only).
[Authorize]
[Route("Users")]
public sealed class UsersController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<UsersController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public UsersController(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<UsersController> logger)
    {
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Governance/Users/Index.cshtml");

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] UserEditViewModel model)
    {
        // Invitation flow: no password is collected. The user sets their own via the emailed link.
        ModelState.Remove(nameof(model.Password));
        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            // WP-INFRA-AUTH-ACCOUNT-KIND-01 — the optional classification travels as the enum NAME or not at all
            // (an empty selection means "leave it Unknown", never a guessed kind). Read from the posted form rather
            // than the view model on purpose: the model's shape belongs to the invitation flow and stays as it is.
            var accountKind = Request.HasFormContentType ? Request.Form["AccountKind"].ToString().Trim() : string.Empty;
            var payload = new
            {
                email = model.Email,
                firstName = model.FirstName,
                lastName = model.LastName,
                accountKind = string.IsNullOrWhiteSpace(accountKind) ? null : accountKind
            };
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/users", payload, _jsonOptions);
            // AuthService returns the set-password link in UserDto.setupUrl ONLY in Development;
            // it is null in Production, so nothing leaks to the UI there.
            return response.IsSuccessStatusCode
                ? Json(new { success = true, setupUrl = await ExtractSetupUrlAsync(response) })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users create failed.");
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] UserEditViewModel model)
    {
        model.Id = id;
        ModelState.Remove(nameof(model.Password)); // password not edited here
        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var payload = new UserUpdatePayload { FirstName = model.FirstName, LastName = model.LastName, IsActive = model.IsActive };
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/users/{id}", payload, _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users edit failed for {UserId}.", id);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpGet("get/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!AddAuthHeaders())
            return Json(new { success = false });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/users/{id}");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false });

            var payload = await response.Content.ReadFromJsonAsync<GovernanceGatewayResponse<UserDetailViewModel>>(_jsonOptions);
            var model = payload?.Data;
            if (model is null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = model.Id,
                    email = model.Email,
                    firstName = model.FirstName,
                    lastName = model.LastName,
                    isActive = model.IsActive,
                    roles = model.Roles,
                    lastLoginAt = model.LastLoginAt,
                    failedLoginAttempts = model.FailedLoginAttempts,
                    mustChangePassword = model.MustChangePassword,
                    mfaStatus = model.MfaStatus
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users get-by-id failed for {UserId}.", id);
            return Json(new { success = false });
        }
    }

    // ── WP-INFRA-AUTH-ACCOUNT-KIND-01 — same-origin API proxy (ProxyAsync shape) ──────────────────────
    // GET  /Users/api/lookup?search=&limit=      → GET  /api/users/lookup            [auth.users.lookup]
    // GET  /Users/api/{id}/account-assertion     → GET  /api/users/{id}/account-assertion [auth.users.lookup]
    // POST /Users/api/{id}/account-kind {kind}   → POST /api/users/{id}/account-kind [auth.users.account-kind.manage]

    [HttpGet("api/lookup")]
    public Task<IActionResult> ApiLookup([FromQuery] string? search, [FromQuery] int? limit)
    {
        var query = $"?search={Uri.EscapeDataString(search ?? string.Empty)}&limit={Math.Clamp(limit ?? 20, 1, 50)}";
        return ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/users/lookup{query}", readBody: false);
    }

    [HttpGet("api/{id:guid}/account-assertion")]
    public Task<IActionResult> ApiAccountAssertion(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/users/{id}/account-assertion", readBody: false);

    [HttpPost("api/{id:guid}/account-kind")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApiSetAccountKind(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/users/{id}/account-kind", readBody: true);

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string targetUrl, bool readBody)
    {
        if (!TryCreateTenantRequest(method, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (readBody)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var relayed = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                    request.Content = new StringContent(relayed, Encoding.UTF8, "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                // The upstream status passes through verbatim: a 403 (key not granted), a 404 (not this
                // tenant's user — deliberately the same body as "does not exist") or a 400 (bad kind) must
                // reach the browser as itself.
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users proxy failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Users dependency unavailable." });
        }
    }

    private bool TryCreateTenantRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        var tenantId = string.IsNullOrWhiteSpace(token) ? null : GetTenantId(token);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, ResolveCorrelationId());
        if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
        }

        return true;
    }

    private string ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return HttpContext.TraceIdentifier;
    }

    // ── Admin actions (proxy → AuthService) ────────────────────────────────────
    [HttpPost("disable/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Disable(Guid id) => ForwardUserActionAsync($"/api/users/{id}/disable", "disable", id);

    [HttpPost("enable/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Enable(Guid id) => ForwardUserActionAsync($"/api/users/{id}/enable", "enable", id);

    [HttpPost("resend-invite/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResendInvite(Guid id) => ForwardUserActionAsync($"/api/users/resend-invite/{id}", "resend-invite", id);

    [HttpPost("reset-password/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResetPassword(Guid id) => ForwardUserActionAsync($"/api/users/{id}/reset-password", "reset-password", id);

    private async Task<IActionResult> ForwardUserActionAsync(string downstreamPath, string action, Guid id)
    {
        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsync($"{_gatewayUrl}{downstreamPath}", content: null);
            // resend-invite / reset-password carry a dev-only setupUrl in the envelope; disable/enable
            // return no body, so ExtractSetupUrlAsync yields null there. Always null in prod.
            return response.IsSuccessStatusCode
                ? Json(new { success = true, setupUrl = await ExtractSetupUrlAsync(response) })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users {Action} failed for {UserId}.", action, id);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    // Reads UserDto.setupUrl from the success envelope ({ data: { ..., setupUrl } }). Returns null
    // when absent (Production) — the dev invite link is never fabricated client-side.
    private async Task<string?> ExtractSetupUrlAsync(HttpResponseMessage response)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw)) return null;
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("setupUrl", out var setupUrl) && setupUrl.ValueKind == JsonValueKind.String)
            {
                var value = setupUrl.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch { }
        return null;
    }

    private List<string> CollectModelErrors() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        return [string.IsNullOrWhiteSpace(message) ? _sharedLocalizer["GatewayError"].Value : message];
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [_sharedLocalizer["Unauthorized"].Value];

        var raw = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
            return [_sharedLocalizer["GatewayError"].Value];

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errorsEl))
            {
                // (1) Response envelope: "errors": [ "..." ]
                if (errorsEl.ValueKind == JsonValueKind.Array)
                {
                    var list = errorsEl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => CleanError(e.GetString()))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!)
                        .ToList();
                    if (list.Count > 0) return list;
                }
                // (2) ProblemDetails validation shape: "errors": { "Field": [ "..." ] }
                else if (errorsEl.ValueKind == JsonValueKind.Object)
                {
                    var list = errorsEl.EnumerateObject()
                        .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                        .SelectMany(p => p.Value.EnumerateArray())
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => CleanError(e.GetString()))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!)
                        .ToList();
                    if (list.Count > 0) return list;
                }
            }

            // (3) ProblemDetails single line: "detail" then "title" — parsed BEFORE the raw fallback
            // so password-policy / duplicate-email errors read cleanly in #formUserAlert.
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(detail.GetString()))
                return [CleanError(detail.GetString())!];
            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(title.GetString()))
                return [CleanError(title.GetString())!];
        }
        catch { }

        return [raw];
    }

    // Strip FluentValidation noise so a single readable line reaches the form alert.
    private static string? CleanError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return message;
        var clean = message
            .Replace("Validation failed:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Severity: Error", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty)
            .Replace("\n", " ")
            .Replace(" --", " ")
            .Trim();
        while (clean.Contains("  ", StringComparison.Ordinal))
            clean = clean.Replace("  ", " ", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(clean) ? message : clean;
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        // BL-294 — read the token through AuthTokenCookies, NEVER Request.Cookies["access_token"] directly.
        // The access token outgrows a single cookie (>3800 chars) and is written in chunks: the base cookie
        // then holds the literal marker "chunks-N" and the token itself lives in access_tokenC1..CN. A direct
        // read therefore sends `Bearer chunks-4` and the gateway 401s. GetAccessToken reassembles the chunks
        // (and returns a short token unchanged), so this call site works in both shapes.
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var tenantId = GetTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    // Resolve the tenant id from the cookie-auth principal first; fall back to decoding the access-token JWT. The
    // cookie principal does not always carry the tenant claim (e.g. platform_admin sessions viewing a tenant), but
    // the bearer JWT does — without this fallback the tenant-scoped admin-action proxy would 401 locally.
    private string? GetTenantId(string? accessToken)
    {
        var claimValue = User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase) ||
            x.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
            return claimValue;

        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
                return null;

            var jwt = handler.ReadJwtToken(accessToken);
            return jwt.Claims.FirstOrDefault(x =>
                string.Equals(x.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
                x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase) ||
                x.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
        }
        catch
        {
            return null;
        }
    }
}
