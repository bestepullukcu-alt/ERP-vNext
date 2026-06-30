using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Services.Auth;
using Diten.Web.Services.Branding;

namespace Diten.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAuthGateway _authGateway;
    private readonly IAuthCookieService _authCookieService;
    private readonly IBrandingGateway _brandingGateway;

    public AccountController(
        IAuthGateway authGateway,
        IAuthCookieService authCookieService,
        IBrandingGateway brandingGateway)
    {
        _authGateway = authGateway;
        _authCookieService = authCookieService;
        _brandingGateway = brandingGateway;
    }

    // FIX-LOGIN-BRIDGE-RESILIENCE — a transport failure (auth service / gateway unreachable) surfaces from the
    // bridge as StatusCode >= 500 with a clean ErrorMessage; map it to 503 so the client shows "service
    // unavailable" instead of a misleading 401. Credential / validation failures keep the existing 401.
    private IActionResult AuthFailureResult(AuthBridgeResult result, string fallbackMessage)
    {
        var detail = result.ErrorMessage ?? fallbackMessage;
        return result.StatusCode is >= 500
            ? StatusCode(503, new { detail })
            : Unauthorized(new { detail });
    }

    [HttpGet("/account/login")]
    public async Task<IActionResult> Login(Guid? tenantId = null, string? returnUrl = null, CancellationToken ct = default)
    {
        if (HasValidActor("tenant_user"))
        {
            return Redirect(returnUrl ?? "/WorkCenter");
        }

        ViewBag.AuthMode = "tenant";
        ViewBag.PostLoginDefault = "/WorkCenter";
        ViewBag.ReturnUrl = returnUrl;

        // Pre-auth theming: when a tenant login link carries ?tenantId=, fetch that tenant's branding
        // so the login screen shows its logo/favicon. Best-effort — any miss leaves the platform default.
        await ApplyTenantBrandingAsync(tenantId, ct);

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
        if (!ModelState.IsValid || request.TenantId == Guid.Empty)
        {
            return BadRequest(new { detail = "Tenant login requires a valid tenant identifier." });
        }

        var result = await _authGateway.LoginTenantAsync(request.Email, request.Password, request.TenantId, request.RememberMe, ct);
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
            return AuthFailureResult(result, "Login failed.");
        }

        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);

        return Ok(new LoginBridgeResponse(
            ResolveReturnUrl(request.ReturnUrl, "/WorkCenter"),
            result.User));
    }

    [HttpPost("/account/login/mfa")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaLoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { detail = "Verification code is required." });
        }

        var result = await _authGateway.VerifyTenantMfaAsync(request.ChallengeId, request.Code, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Verification failed.");
        }

        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
        return Ok(new LoginBridgeResponse(
            ResolveReturnUrl(request.ReturnUrl, "/WorkCenter"),
            result.User));
    }

    [HttpPost("/account/login/mfa/resend")]
    public async Task<IActionResult> ResendMfa([FromBody] MfaResendRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.ChallengeId))
        {
            return BadRequest(new { detail = "Verification challenge is required." });
        }

        var result = await _authGateway.ResendTenantMfaAsync(request.ChallengeId, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.ChallengeId))
        {
            return AuthFailureResult(result, "Verification code could not be resent.");
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
            return BadRequest(new { detail = "Platform login request is invalid." });
        }

        var result = await _authGateway.LoginPlatformAsync(request.Email, request.Password, request.RememberMe, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Platform login failed.");
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
            return BadRequest(new { detail = "Email is required." });
        }

        await _authGateway.ForgotPlatformPasswordAsync(request.Email, ct);
        return Ok(new { success = true });
    }

    [HttpGet("/platform/reset-password")]
    public IActionResult PlatformResetPassword([FromQuery] string? email, [FromQuery] string? token)
    {
        ViewBag.AuthMode = "platform";
        ViewBag.Email = email ?? string.Empty;
        ViewBag.Token = token ?? string.Empty;
        ViewBag.IsForcedChange = false;
        ViewBag.SetPasswordUrl = "/platform/reset-password";
        ViewBag.BackToLoginUrl = "/platform/login";
        return View("ResetPassword");
    }

    // Tenant invitation redemption — reuses the ResetPassword view/JS with tenant wiring.
    [HttpGet("/account/set-password")]
    public IActionResult SetPassword([FromQuery] string? email, [FromQuery] string? token)
    {
        ViewBag.AuthMode = "tenant";
        ViewBag.Email = email ?? string.Empty;
        ViewBag.Token = token ?? string.Empty;
        ViewBag.IsForcedChange = false;
        ViewBag.SetPasswordUrl = "/account/set-password";
        ViewBag.BackToLoginUrl = "/account/login";
        return View("ResetPassword");
    }

    [HttpPost("/account/set-password")]
    public async Task<IActionResult> SetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { detail = "Password setup request is invalid." });
        }

        var result = await _authGateway.ResetTenantPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
        return result.Success
            ? Ok(new { success = true, redirectUrl = "/account/login" })
            : BadRequest(new { detail = result.ErrorMessage ?? "Password setup link is invalid or expired." });
    }

    [HttpPost("/platform/reset-password")]
    public async Task<IActionResult> PlatformResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { detail = "Password reset request is invalid." });
        }

        var result = await _authGateway.ResetPlatformPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
        return result.Success
            ? Ok(new { success = true, redirectUrl = "/platform/login" })
            : BadRequest(new { detail = result.ErrorMessage ?? "Password reset token is invalid or expired." });
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
            return BadRequest(new { detail = "Password change request is invalid." });
        }

        var result = await _authGateway.ChangePlatformPasswordAsync(request.CurrentPassword, request.NewPassword, request.RememberMe, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return AuthFailureResult(result, "Password change failed.");
        }

        _authCookieService.ClearTokens(Response);
        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
        return Ok(new { success = true, redirectUrl = "/Platform/Tenants" });
    }

    [HttpPost("/account/refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!TryGetCookie("access_token", out var accessToken) || !TryGetCookie("refresh_token", out var refreshToken))
        {
            _authCookieService.ClearTokens(Response);
            return Unauthorized(new { detail = "Authentication cookies are missing." });
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
                return StatusCode(result.StatusCode ?? 500, new { 
                    detail = result.ErrorMessage ?? "Refresh failed.",
                    reauthRequired = result.ReauthRequired
                });
            }

            _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
            return Ok(new { success = true, user = result.User });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                detail = "Transient error during token refresh: " + ex.Message,
                reauthRequired = false
            });
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
