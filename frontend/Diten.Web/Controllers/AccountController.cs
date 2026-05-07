using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Services.Auth;

namespace Diten.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAuthGateway _authGateway;
    private readonly IAuthCookieService _authCookieService;

    public AccountController(IAuthGateway authGateway, IAuthCookieService authCookieService)
    {
        _authGateway = authGateway;
        _authCookieService = authCookieService;
    }

    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HasValidActor("tenant_user"))
        {
            return Redirect(returnUrl ?? "/WorkCenter");
        }

        ViewBag.AuthMode = "tenant";
        ViewBag.PostLoginDefault = "/WorkCenter";
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost("/account/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.TenantId == Guid.Empty)
        {
            return BadRequest(new { detail = "Tenant login requires a valid tenant identifier." });
        }

        var result = await _authGateway.LoginTenantAsync(request.Email, request.Password, request.TenantId, ct);
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
            return Unauthorized(new { detail = result.ErrorMessage ?? "Login failed." });
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
            return Unauthorized(new { detail = result.ErrorMessage ?? "Verification failed." });
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
            return Unauthorized(new { detail = result.ErrorMessage ?? "Verification code could not be resent." });
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

        var result = await _authGateway.LoginPlatformAsync(request.Email, request.Password, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            return Unauthorized(new { detail = result.ErrorMessage ?? "Platform login failed." });
        }

        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);

        return Ok(new LoginBridgeResponse(
            ResolveReturnUrl(request.ReturnUrl, "/Platform/Tenants"),
            result.User));
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
        var result = await _authGateway.RefreshAsync(accessToken, refreshToken, tenantId, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || !result.ExpiresAt.HasValue)
        {
            _authCookieService.ClearTokens(Response);
            return Unauthorized(new { detail = result.ErrorMessage ?? "Refresh failed." });
        }

        _authCookieService.WriteTokens(Response, result.AccessToken, result.RefreshToken, result.ExpiresAt.Value);
        return Ok(new { success = true, user = result.User });
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
        value = Request.Cookies[cookieName] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
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

    public sealed record LoginRequest(string Email, string Password, Guid TenantId, string? ReturnUrl);
    public sealed record MfaLoginRequest(string ChallengeId, string Code, string? ReturnUrl);
    public sealed record MfaResendRequest(string ChallengeId);
    public sealed record PlatformLoginRequest(string Email, string Password, string? ReturnUrl);
    public sealed record LoginBridgeResponse(
        string RedirectUrl,
        AuthBridgeUser? User,
        bool RequiresMfa = false,
        string? ChallengeId = null,
        string? MaskedDestination = null,
        string? Channel = null,
        DateTime? MfaExpiresAt = null);
}
