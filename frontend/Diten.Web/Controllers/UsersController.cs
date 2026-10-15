using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.Governance;
using Diten.Web.Services.Auth;
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
// WP-AUTH-USER-KIND-UPDATE-01 — Edit forwards it too (the edit form's select, saved by "Update"); AuthService
// refuses a kind CHANGE without the same key, and takes it through the same writer + audit row as account-kind.
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
        if (FormRefusal(model, isCreate: true) is { } refused)
            return refused;

        if (!AddAuthHeaders())
            return NotSignedIn();

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
                : await GatewayFailureAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users create failed.");
            return GatewayUnreachable();
        }
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] UserEditViewModel model)
    {
        model.Id = id;
        ModelState.Remove(nameof(model.Password)); // password not edited here
        if (FormRefusal(model, isCreate: false) is { } refused)
            return refused;

        if (!AddAuthHeaders())
            return NotSignedIn();

        try
        {
            var payload = new UserUpdatePayload
            {
                FirstName = model.FirstName ?? string.Empty,
                LastName = model.LastName ?? string.Empty,
                IsActive = model.IsActive, // ALWAYS sent: AuthService reads a missing isActive as "leave it as it is"
                AccountKind = ReadEditAccountKind()
            };
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/users/{id}", payload, _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : await GatewayFailureAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users edit failed for {UserId}.", id);
            return GatewayUnreachable();
        }
    }

    /*
     * WP-AUTH-USER-KIND-UPDATE-01 — what the edit form says about the kind, as the enum NAME or null.
     * ABSENT field (Razor drew no select: the reader lacks auth.users.account-kind.manage) ⇒ null ⇒ AuthService
     * leaves the kind alone. PRESENT and empty ⇒ "Unknown": on edit the select's empty option is a real choice
     * (back to unconfirmed), not "unset" as on create — dropping it would silently keep a Human/Service kind.
     */
    private string? ReadEditAccountKind()
    {
        if (!Request.HasFormContentType || !Request.Form.ContainsKey("AccountKind"))
            return null;

        var value = Request.Form["AccountKind"].ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
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
                    mfaStatus = model.MfaStatus,
                    accountKind = model.AccountKind,
                    // Finding 22: the DERIVED status (Invited · Inactive · Active) must reach the edit form, or it
                    // falls back to IsActive and draws the activation switch for an invited account.
                    status = model.Status
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
            return WithStatus(NotSignedIn(), StatusCodes.Status401Unauthorized);
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

                // A REFUSAL keeps its upstream status — a 403 (key not granted), a 404 (not this tenant's user) or
                // a 400 (bad kind) reaches the browser as itself — but not its body: the codes travel, the service's
                // text (on a 5xx, an exception message) goes to the log like every other refusal of this proxy.
                if (!response.IsSuccessStatusCode)
                {
                    return WithStatus(GatewayRefusalResult(response.StatusCode, content), (int)response.StatusCode);
                }

                // A success passes through verbatim.
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
            // The path only: the lookup's query string carries what the reader typed into the search box.
            _logger.LogError(ex, "Users proxy failed for {Method} {TargetPath}.", method, new Uri(targetUrl).AbsolutePath);
            return WithStatus(GatewayUnreachable(), StatusCodes.Status503ServiceUnavailable);
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
            return NotSignedIn();

        try
        {
            var response = await _httpClient.PostAsync($"{_gatewayUrl}{downstreamPath}", content: null);
            // resend-invite / reset-password carry a dev-only setupUrl in the envelope; disable/enable
            // return no body, so ExtractSetupUrlAsync yields null there. Always null in prod.
            return response.IsSuccessStatusCode
                ? Json(new { success = true, setupUrl = await ExtractSetupUrlAsync(response) })
                : await GatewayFailureAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Users {Action} failed for {UserId}.", action, id);
            return GatewayUnreachable();
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

    /*
     * WP-USERS-ERROR-CODES-01 — WHAT A REFUSAL LOOKS LIKE TO THE SCREEN. Never a sentence this proxy did not write:
     *   errorCodes   every stable code of the refusal ({ code, params }) — index.js says each in the reader's language
     *   errorCode / errorParams  the first of them (the shape the screen read before; kept)
     *   uncoded      true when a failure came without a code — the screen adds its general sentence, nothing is dropped
     *   status       the upstream HTTP status (null when the refusal is this proxy's own)
     *   ownMessages  this proxy's own localized sentences, and nothing else. The only two callers are NotSignedIn and
     *                GatewayUnreachable below; there is NO `errors` field — the screen does not read one, so a
     *                service's text has no door to the reader even if a later change tried to pass it along.
     * A service's English text and an exception's message (which can name an internal host:port) go to the server
     * log, not to the browser.
     */
    private JsonResult Refusal(IReadOnlyList<GatewayErrorCode> codes, bool uncoded, int? status, params LocalizedString[] ownMessages)
        => Json(new
        {
            success = false,
            ownMessages = ownMessages.Select(m => m.Value).ToArray(),
            errorCode = codes.Count > 0 ? codes[0].Code : null,
            errorParams = codes.Count > 0 ? codes[0].Params : null,
            errorCodes = codes.Select(c => new { code = c.Code, @params = c.Params }).ToArray(),
            uncoded,
            status
        });

    private JsonResult NotSignedIn(int? status = null) => Refusal([], uncoded: false, status, _sharedLocalizer["Unauthorized"]);

    // The gateway could not be reached (or answered something that threw): the proxy's own sentence. The exception
    // itself is logged by the caller — its message never travels.
    private JsonResult GatewayUnreachable() => Refusal([], uncoded: false, status: null, _sharedLocalizer["GatewayError"]);

    private static JsonResult WithStatus(JsonResult result, int httpStatus)
    {
        result.StatusCode = httpStatus;
        return result;
    }

    /*
     * The form's own check, answered with the codes AuthService's validators use for the same rules (the screen has
     * their sentences in seven languages) instead of MVC's English DataAnnotations text. A ModelState error is, by
     * construction, one this check does not know (the view model carries no validation attributes): it is ALWAYS
     * announced as uncoded, also next to codes — the general sentence, never an English one, never silence.
     */
    private JsonResult? FormRefusal(UserEditViewModel model, bool isCreate)
    {
        var codes = FormRefusalCodes(model, isCreate).Select(code => new GatewayErrorCode(code, null)).ToList();
        return codes.Count == 0 && ModelState.IsValid ? null : Refusal(codes, uncoded: !ModelState.IsValid, status: null);
    }

    // The e-mail is judged on CREATE only: an update never sends it (AuthService's UpdateUserCommand has no e-mail),
    // so judging it on edit would lock the NAME of an older account whose address is not address-shaped.
    [NonAction]
    public static IEnumerable<string> FormRefusalCodes(UserEditViewModel model, bool isCreate)
    {
        if (isCreate)
        {
            if (string.IsNullOrWhiteSpace(model.Email)) yield return "USER_EMAIL_REQUIRED";
            else
            {
                if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.Email)) yield return "USER_EMAIL_INVALID";
                if (model.Email.Length > UserEditViewModel.EmailMaxLength) yield return "USER_EMAIL_TOO_LONG";
            }
        }

        if (string.IsNullOrWhiteSpace(model.FirstName)) yield return "USER_FIRST_NAME_REQUIRED";
        else if (model.FirstName.Length > UserEditViewModel.NameMaxLength) yield return "USER_FIRST_NAME_TOO_LONG";

        if (string.IsNullOrWhiteSpace(model.LastName)) yield return "USER_LAST_NAME_REQUIRED";
        else if (model.LastName.Length > UserEditViewModel.NameMaxLength) yield return "USER_LAST_NAME_TOO_LONG";
    }

    // A refused hop: the codes travel on, the service's text stays in the log (BL-459 — a code's own string params,
    // e.g. USER_QUOTA_EXCEEDED { max, current }, travel with it).
    private async Task<IActionResult> GatewayFailureAsync(HttpResponseMessage response)
        => GatewayRefusalResult(response.StatusCode, await response.Content.ReadAsStringAsync());

    private JsonResult GatewayRefusalResult(System.Net.HttpStatusCode statusCode, string raw)
    {
        var status = (int)statusCode;
        if (statusCode == System.Net.HttpStatusCode.Unauthorized)
            return NotSignedIn(status);

        var refusal = GatewayRefusal.Read(raw);
        _logger.LogWarning("Users gateway refusal. Status={Status} Codes={Codes} Body={Body}",
            status, string.Join(",", refusal.Codes.Select(c => c.Code)), raw.Length > 2000 ? raw[..2000] : raw);
        return Refusal(refusal.Codes, refusal.HasUncoded, status);
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
