using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Web.Filters;

public sealed class ShellAccessFilter : IAuthorizationFilter
{
    private static readonly string[] PlatformActors = { "platform_admin", "partner_admin" };
    private const string ReferenceDataPath = "/Platform/ReferenceData";
    private readonly IConfiguration _configuration;

    public ShellAccessFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
        {
            return;
        }

        var request = context.HttpContext.Request;
        var path = request.Path;

        if (path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureJwtCookiePrincipal(context.HttpContext);

        var actorType = context.HttpContext.User.FindFirst("actor_type")?.Value?.Trim().ToLowerInvariant();
        var isPlatformPath = path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWithSegments("/api/platform", StringComparison.OrdinalIgnoreCase) ||
                             request.Host.Host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);

        if (isPlatformPath)
        {
            if (string.IsNullOrWhiteSpace(actorType))
            {
                var loginPath = IsReferenceDataPath(path) ? "/account/login" : "/platform/login";
                context.Result = BuildLoginRedirect(loginPath, request);
                return;
            }

            if (IsReferenceDataPath(path) && string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Array.IndexOf(PlatformActors, actorType) < 0)
            {
                context.Result = new ForbidResult();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(actorType))
        {
            context.Result = BuildLoginRedirect("/account/login", request);
            return;
        }

        if (!string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
        }
    }

    private static bool IsReferenceDataPath(Microsoft.AspNetCore.Http.PathString path) =>
        path.StartsWithSegments(ReferenceDataPath, StringComparison.OrdinalIgnoreCase);

    private static RedirectResult BuildLoginRedirect(string loginPath, Microsoft.AspNetCore.Http.HttpRequest request)
    {
        var returnUrl = request.Path + request.QueryString;
        return new RedirectResult($"{loginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    private void EnsureJwtCookiePrincipal(Microsoft.AspNetCore.Http.HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !string.IsNullOrWhiteSpace(context.User.FindFirst("actor_type")?.Value))
        {
            return;
        }

        var accessToken = AuthTokenCookies.GetAccessToken(context.Request);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var jwtSecret = _configuration["JwtSettings:Secret"] ?? string.Empty;
        var jwtIssuer = _configuration["JwtSettings:Issuer"] ?? string.Empty;
        var jwtAudience = _configuration["JwtSettings:Audience"] ?? string.Empty;

        Console.WriteLine($"[SHELL_FILTER_DEBUG] Secret length: {jwtSecret.Length}, Issuer: '{jwtIssuer}', Audience: '{jwtAudience}', AccessToken empty: {string.IsNullOrEmpty(accessToken)}");

        if (string.IsNullOrWhiteSpace(jwtSecret) ||
            string.IsNullOrWhiteSpace(jwtIssuer) ||
            string.IsNullOrWhiteSpace(jwtAudience))
        {
            return;
        }

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            context.User = principal;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SHELL_FILTER_ERROR] Token validation failed: {ex.Message}");
            AuthTokenCookies.ClearTokens(context.Response);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
