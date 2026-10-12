using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Diten.Web.Services.Auth;
using Diten.Web.Services.Branding;

namespace Diten.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    // FIX-LOGIN-TENANT-FALLBACK — the seeded DefaultTenant. ONLY used as a fallback in Development so a developer
    // can sign in via a plain /account/login (no invite link). Production NEVER falls back — an explicit tenant
    // is still required.
    private static readonly Guid DevDefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IAuthGateway _authGateway;
    private readonly IAuthCookieService _authCookieService;
    private readonly IBrandingGateway _brandingGateway;
    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthGateway authGateway,
        IAuthCookieService authCookieService,
        IBrandingGateway brandingGateway,
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AccountController>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance;
        _authGateway = authGateway;
        _authCookieService = authCookieService;
        _brandingGateway = brandingGateway;
        _environment = environment;
        _localizer = localizer;
    }

    // WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496) — EVERY refusal of these doors is said in the reader's language: the
    // service's sentence when it came with a code the bridge knows (already localized), otherwise this door's own
    // sentence — never the service's English. The body is the shape diten-refusal.js reads ({ errors, local: true }: a
    // sentence this application produced, already localized), with `detail` kept for the existing readers.
    private ObjectResult Refused(int status, AuthBridgeResult? result, string fallbackKey)
        => StatusCode(status, new { detail = SentenceOf(result, fallbackKey), errors = new[] { SentenceOf(result, fallbackKey) }, local = true });

    private string SentenceOf(AuthBridgeResult? result, string fallbackKey)
        => result is { ErrorLocalized: true } && !string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result.ErrorMessage
            : Say(fallbackKey);

    // FIX2 item 6 — the refresh answers like every other door (the reader's language, the shape diten-refusal.js reads)
    // and keeps `reauthRequired`, which dt-defaults' refresh reads; the service's English and an exception's text never
    // reach the browser.
    private ObjectResult RefreshRefused(int status, AuthBridgeResult? result, string fallbackKey, bool reauthRequired)
    {
        var detail = SentenceOf(result, fallbackKey);
        return StatusCode(status, new { detail, errors = new[] { detail }, local = true, reauthRequired });
    }

    private string Say(string key)
    {
        var sentence = _localizer[key];
        return sentence.ResourceNotFound ? _localizer["ErrorOccurred"].Value : sentence.Value;
    }

    private ObjectResult RequestInvalid() => Refused(StatusCodes.Status400BadRequest, null, "Auth.Error.RequestInvalid");

    // BL-529 FIX3 — the set-password link doors keep the service's status where it means something to the page (429 too
    // many requests, 409 a deactivated account); the localized sentence travels in `detail`.
    private IActionResult PasswordDoorFailure(AuthBridgeResult result, string fallbackKey)
        => result.StatusCode switch
        {
            429 => Refused(429, result, "Auth.Error.TooManyRequests"),
            409 => Refused(409, result, fallbackKey),
            >= 500 => Refused(503, result, "Auth.Error.ServiceUnavailable"),
            _ => Refused(400, result, fallbackKey)
        };

    // FIX-LOGIN-BRIDGE-RESILIENCE — a transport failure (auth service / gateway unreachable) surfaces from the
    // bridge as StatusCode >= 500 with a clean ErrorMessage; map it to 503 so the client shows "service
    // unavailable" instead of a misleading 401. Credential / validation failures keep the existing 401.
    private IActionResult AuthFailureResult(AuthBridgeResult result, string fallbackKey)
        => result.StatusCode is >= 500
            ? Refused(503, result, "Auth.Error.ServiceUnavailable")
            : Refused(401, result, fallbackKey);

    [HttpGet("/account/login")]
    public async Task<IActionResult> Login(Guid? tenantId = null, string? returnUrl = null, CancellationToken ct = default)
    {
        if (HasValidActor("tenant_user"))
        {
            return Redirect(returnUrl ?? "/WorkCenterNext");
        }

        ViewBag.AuthMode = "tenant";
        ViewBag.PostLoginDefault = "/WorkCenterNext";
        ViewBag.ReturnUrl = returnUrl;

        // FIX-LOGIN-TENANT-FALLBACK — in Development only, expose the DefaultTenant id to the page so a plain
        // /account/login (no ?tenantId=) can still sign in. Stays null in Production (explicit tenant required).
        if (_environment.IsDevelopment())
        {
            ViewBag.DevDefaultTenantId = DevDefaultTenantId;
        }

        // Pre-auth theming: when a tenant login link carries ?tenantId=, fetch that tenant's branding so the login
        // screen shows its logo/favicon. In Development a missing tenantId falls back to the DefaultTenant's branding
        // so the dev login screen is not blank. Best-effort — any miss leaves the platform default.
        var brandingTenantId = tenantId
            ?? (_environment.IsDevelopment() ? DevDefaultTenantId : (Guid?)null);
        await ApplyTenantBrandingAsync(brandingTenantId, ct);

        return View();
    }

    private async Task ApplyTenantBrandingAsync(Guid? tenantId, CancellationToken ct)
    {
        if (tenantId is not { } id || id == Guid.Empty)
        {
            return;
        }

        var branding = await _brandingGateway.GetTenantBrandingAsync(id, ct);
        if (branding is null)
        {
            return;
        }

        ViewBag.BrandingLogoDataUrl = branding.LogoDataUrl;
        ViewBag.BrandingFaviconDataUrl = branding.FaviconDataUrl;
        ViewBag.BrandingDisplayName = branding.DisplayName;
    }

    [HttpPost("/account/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // FIX-LOGIN-TENANT-FALLBACK — defensive: in Development a missing tenant resolves to the DefaultTenant so a
        // plain /account/login works. Production keeps requiring an explicit tenant.
        var effectiveTenantId = request.TenantId;
        if (effectiveTenantId == Guid.Empty && _environment.IsDevelopment())
        {
            effectiveTenantId = DevDefaultTenantId;
        }

        if (!ModelState.IsValid || effectiveTenantId == Guid.Empty)
        {
            return RequestInvalid();
        }

        var result = await _authGateway.LoginTenantAsync(request.Email, request.Password, effectiveTenantId, request.RememberMe, ct);
        if (result.RequiresMfa && !string.IsNullOrWhiteSpace(result.ChallengeId))
        {
            return Ok(new LoginBridgeResponse(
                string.Empty,
                null,
                true,
                result.ChallengeId,
                result.MaskedDestination,
                result.Channel,
                result.MfaExpiresAt));
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Auth.Error.LoginFailed");
        }

        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);

        // FIX-TENANT-MUSTCHANGEPW — a temp-password tenant user must change it before anything else (mirrors platform).
        return Ok(new LoginBridgeResponse(
            result.RequiresPasswordChange ? "/account/change-password" : ResolveReturnUrl(request.ReturnUrl, "/WorkCenterNext"),
            result.User,
            RequiresPasswordChange: result.RequiresPasswordChange));
    }

    [HttpPost("/account/login/mfa")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaLoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Code))
        {
            return RequestInvalid();
        }

        var result = await _authGateway.VerifyTenantMfaAsync(request.ChallengeId, request.Code, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Auth.Error.VerificationFailed");
        }

        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
        return Ok(new LoginBridgeResponse(
            ResolveReturnUrl(request.ReturnUrl, "/WorkCenterNext"),
            result.User));
    }

    [HttpPost("/account/login/mfa/resend")]
    public async Task<IActionResult> ResendMfa([FromBody] MfaResendRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.ChallengeId))
        {
            return RequestInvalid();
        }

        var result = await _authGateway.ResendTenantMfaAsync(request.ChallengeId, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.ChallengeId))
        {
            return AuthFailureResult(result, "Auth.Error.VerificationFailed");
        }

        return Ok(new LoginBridgeResponse(
            string.Empty,
            null,
            true,
            result.ChallengeId,
            result.MaskedDestination,
            result.Channel,
            result.MfaExpiresAt));
    }

    [HttpGet("/platform/login")]
    public IActionResult PlatformLogin(string? returnUrl = null)
    {
        if (HasValidActor("platform_admin", "partner_admin"))
        {
            return Redirect(returnUrl ?? "/Platform/Tenants");
        }

        ViewBag.AuthMode = "platform";
        ViewBag.PostLoginDefault = "/Platform/Tenants";
        ViewBag.ReturnUrl = returnUrl;
        return View("Login");
    }

    [HttpPost("/platform/login")]
    public async Task<IActionResult> PlatformLogin([FromBody] PlatformLoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return RequestInvalid();
        }

        var result = await _authGateway.LoginPlatformAsync(request.Email, request.Password, request.RememberMe, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Auth.Error.LoginFailed");
        }

        _authCookieService.ClearTokens(Response);
        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);

        return Ok(new LoginBridgeResponse(
            result.RequiresPasswordChange ? "/platform/change-password" : ResolveReturnUrl(request.ReturnUrl, "/Platform/Tenants"),
            result.User,
            RequiresPasswordChange: result.RequiresPasswordChange));
    }

    [HttpGet("/platform/forgot-password")]
    public IActionResult PlatformForgotPassword()
    {
        ViewBag.AuthMode = "platform";
        return View("ForgotPassword");
    }

    [HttpPost("/platform/forgot-password")]
    public async Task<IActionResult> PlatformForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return RequestInvalid();
        }

        // BL-529 FIX3 — a refusal the person must see (too many requests, the service down) is passed on in their language;
        // every other answer stays "sent" (the door never says whether the address exists).
        var result = await _authGateway.ForgotPlatformPasswordAsync(request.Email, ct);
        if (!result.Success && result.StatusCode is 429 or >= 500)
        {
            return result.StatusCode == 429
                ? Refused(429, result, "Auth.Error.TooManyRequests")
                : Refused(503, result, "Auth.Error.ServiceUnavailable");
        }

        return Ok(new { success = true });
    }

    [HttpGet("/platform/reset-password")]
    public IActionResult PlatformResetPassword([FromQuery] string? email, [FromQuery] string? token)
    {
        KeepLinkOutOfReferrerAndCache();
        ViewBag.AuthMode = "platform";
        ViewBag.Email = email ?? string.Empty;
        ViewBag.Token = token ?? string.Empty;
        ViewBag.IsForcedChange = false;
        ViewBag.SetPasswordUrl = "/platform/reset-password";
        ViewBag.BackToLoginUrl = "/platform/login";
        return View("ResetPassword");
    }

    // Tenant invitation redemption — reuses the ResetPassword view/JS with tenant wiring.
    // WP-USERS-ROLES-CLOSE-FIX1 item 3 (KR4) — the link is checked BEFORE the form is drawn (read-only, AuthService's
    // set-password/check, the redemption's own reader): a deactivated account's link and a dead link get no form, only
    // their sentence. Two faces, never more than the redemption would say: "deactivated" (the link is still the account's —
    // a pending invitation) and one generic "this link is no longer valid" for everything else (missing, expired, used, or
    // deleted by a deactivation — a deleted link is never called "deactivated"). The service unreachable draws the form as
    // before: the redemption then answers for itself. The link is not logged here.
    [HttpGet("/account/set-password")]
    public async Task<IActionResult> SetPassword([FromQuery] string? email, [FromQuery] string? token, CancellationToken ct)
    {
        KeepLinkOutOfReferrerAndCache();
        ViewBag.LinkRefusal = await SetPasswordLinkRefusalAsync(email, token, ct);
        ViewBag.AuthMode = "tenant";
        ViewBag.Email = email ?? string.Empty;
        ViewBag.Token = token ?? string.Empty;
        ViewBag.IsForcedChange = false;
        ViewBag.SetPasswordUrl = "/account/set-password";
        ViewBag.BackToLoginUrl = "/account/login";
        return View("ResetPassword");
    }

    // FIX2 small item — a page opened from a set-password link carries the link in its address: no Referer leaves it (to a
    // font, an image, the next site) and no cache keeps it.
    private void KeepLinkOutOfReferrerAndCache()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private async Task<string?> SetPasswordLinkRefusalAsync(string? email, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return Say("Auth.Error.LinkInvalid");
        }

        var check = await _authGateway.CheckTenantPasswordLinkAsync(email, token, ct);
        return check.StatusCode switch
        {
            _ when check.Success => null,
            409 => Say("Auth.Error.AccountDeactivated"),
            429 => Say("Auth.Error.TooManyRequests"),
            >= 500 => null,
            _ => Say("Auth.Error.LinkInvalid")
        };
    }

    [HttpPost("/account/set-password")]
    public async Task<IActionResult> SetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return RequestInvalid();
        }

        var result = await _authGateway.ResetTenantPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
        return result.Success
            ? Ok(new { success = true, redirectUrl = "/account/login" })
            : PasswordDoorFailure(result, "Auth.Error.LinkInvalid");
    }

    [HttpPost("/platform/reset-password")]
    public async Task<IActionResult> PlatformResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return RequestInvalid();
        }

        var result = await _authGateway.ResetPlatformPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
        return result.Success
            ? Ok(new { success = true, redirectUrl = "/platform/login" })
            : PasswordDoorFailure(result, "Auth.Error.LinkInvalid");
    }

    [HttpGet("/platform/change-password")]
    public IActionResult PlatformChangePassword()
    {
        ViewBag.AuthMode = "platform";
        ViewBag.Email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;
        ViewBag.Token = string.Empty;
        ViewBag.IsForcedChange = true;
        ViewBag.SetPasswordUrl = "/platform/change-password";
        ViewBag.BackToLoginUrl = "/platform/login";
        return View("ResetPassword");
    }

    [HttpPost("/platform/change-password")]
    public async Task<IActionResult> PlatformChangePassword([FromBody] ForcedChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return RequestInvalid();
        }

        var result = await _authGateway.ChangePlatformPasswordAsync(request.CurrentPassword, request.NewPassword, request.RememberMe, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Auth.Error.PasswordChangeFailed");
        }

        _authCookieService.ClearTokens(Response);
        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
        return Ok(new { success = true, redirectUrl = "/Platform/Tenants" });
    }

    // FIX-TENANT-MUSTCHANGEPW — tenant forced first-login change (mirror of /platform/change-password). Reuses the
    // shared ResetPassword view in forced-change mode, pointed at the tenant endpoints.
    [HttpGet("/account/change-password")]
    public IActionResult ChangePassword()
    {
        ViewBag.AuthMode = "tenant";
        ViewBag.Email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;
        ViewBag.Token = string.Empty;
        ViewBag.IsForcedChange = true;
        ViewBag.SetPasswordUrl = "/account/change-password";
        ViewBag.BackToLoginUrl = "/account/login";
        return View("ResetPassword");
    }

    [HttpPost("/account/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ForcedChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return RequestInvalid();
        }

        var result = await _authGateway.ChangeTenantPasswordAsync(request.CurrentPassword, request.NewPassword, request.RememberMe, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Auth.Error.PasswordChangeFailed");
        }

        _authCookieService.ClearTokens(Response);
        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
        return Ok(new { success = true, redirectUrl = "/WorkCenterNext" });
    }

    [HttpPost("/account/refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!TryGetCookie("access_token", out var accessToken) || !TryGetCookie("refresh_token", out var refreshToken))
        {
            _authCookieService.ClearTokens(Response);
            return RefreshRefused(StatusCodes.Status401Unauthorized, null, "Auth.Error.SessionEnded", reauthRequired: true);
        }

        var tenantId = TryReadTenantId(accessToken);
        try
        {
            var result = await _authGateway.RefreshAsync(accessToken, refreshToken, tenantId, ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
            {
                if (result.ReauthRequired)
                {
                    _authCookieService.ClearTokens(Response);
                }
                var status = result.StatusCode ?? StatusCodes.Status500InternalServerError;
                return RefreshRefused(status, result,
                    status >= 500 ? "Auth.Error.ServiceUnavailable" : "Auth.Error.SessionEnded", result.ReauthRequired);
            }

            _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
            return Ok(new { success = true, user = result.User });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The exception is the server's to read (its type, in the log); the browser gets the general sentence.
            _logger.LogWarning(ex, "Token refresh failed with {ExceptionType}; the session is kept.", ex.GetType().Name);
            return RefreshRefused(StatusCodes.Status500InternalServerError, null, "Auth.Error.ServiceUnavailable", reauthRequired: false);
        }
    }

    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var redirectUrl = ResolveLogoutFallbackUrl(returnUrl);

        if (TryGetCookie("access_token", out var accessToken) && TryGetCookie("refresh_token", out var refreshToken))
        {
            redirectUrl = ResolveLogoutRedirectUrl(accessToken);
            var tenantId = TryReadTenantId(accessToken);
            try
            {
                await _authGateway.LogoutAsync(accessToken, refreshToken, tenantId, ct);
            }
            catch
            {
                // We still clear cookies to terminate the session locally.
            }
        }

        _authCookieService.ClearTokens(Response);

        var isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        var expectsJson = Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
        if (isAjaxRequest || expectsJson)
        {
            return Ok(new { success = true, redirectUrl });
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("/account/logout")]
    public IActionResult LogoutGet([FromQuery] string? returnUrl)
    {
        var redirectUrl = TryGetCookie("access_token", out var accessToken)
            ? ResolveLogoutRedirectUrl(accessToken)
            : ResolveLogoutFallbackUrl(returnUrl);

        return Redirect(redirectUrl);
    }

    private bool HasValidActor(params string[] allowedActors)
    {
        var actorType = User.FindFirst("actor_type")?.Value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(actorType))
        {
            return false;
        }

        return allowedActors.Any(a => string.Equals(a, actorType, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetCookie(string cookieName, out string value)
    {
        return AuthTokenCookies.TryGet(Request, cookieName, out value);
    }

    private static Guid? TryReadTenantId(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return null;
            }

            var token = handler.ReadJwtToken(accessToken);
            var claimValue = token.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

            return Guid.TryParse(claimValue, out var tenantId) ? tenantId : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveLogoutRedirectUrl(string accessToken)
    {
        var actorType = TryReadActorType(accessToken);
        return IsPlatformActor(actorType) ? "/platform/login" : "/account/login";
    }

    private static string ResolveLogoutFallbackUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) &&
            (returnUrl.Equals("/platform/login", StringComparison.OrdinalIgnoreCase) ||
             returnUrl.Equals("/account/login", StringComparison.OrdinalIgnoreCase)))
        {
            return returnUrl;
        }

        return "/account/login";
    }

    private static string? TryReadActorType(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return null;
            }

            return handler.ReadJwtToken(accessToken)
                .Claims
                .FirstOrDefault(c => string.Equals(c.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReturnUrl(string? returnUrl, string defaultPath)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            return returnUrl;
        }

        return defaultPath;
    }

    public sealed record LoginRequest(string Email, string Password, Guid TenantId, string? ReturnUrl, bool RememberMe = false);
    public sealed record MfaLoginRequest(string ChallengeId, string Code, string? ReturnUrl);
    public sealed record MfaResendRequest(string ChallengeId);
    public sealed record PlatformLoginRequest(string Email, string Password, string? ReturnUrl, bool RememberMe = false);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword);
    public sealed record ForcedChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword, bool RememberMe = false);
    public sealed record LoginBridgeResponse(
        string RedirectUrl,
        AuthBridgeUser? User,
        bool RequiresMfa = false,
        string? ChallengeId = null,
        string? MaskedDestination = null,
        string? Channel = null,
        DateTime? MfaExpiresAt = null,
        bool RequiresPasswordChange = false);
}
