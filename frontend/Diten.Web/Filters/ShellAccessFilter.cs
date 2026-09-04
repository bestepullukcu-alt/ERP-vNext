using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Web.Filters;

public sealed class ShellAccessFilter : IAuthorizationFilter
{
    private static readonly string[] PlatformActors = { "platform_admin", "partner_admin" };
    private const string ReferenceDataPath = "/Platform/ReferenceData";
    // MOD-0023 — Workflow admin is a tenant-scoped screen that lives under /Platform but is reached by
    // tenant_user actors (like ReferenceData). It must be exempt from the platform-actor-only gate.
    private const string WorkflowPath = "/Platform/Workflow";
    private const string PersonReferencesPath = "/Platform/PersonReferences";

    private const string SecretKey = "JwtSettings:Secret";
    private const string IssuerKey = "JwtSettings:Issuer";
    private const string AudienceKey = "JwtSettings:Audience";

    /// <summary>
    /// Everything this filter needs before it can VERIFY anything. Program.cs asserts these at startup.
    /// </summary>
    private static readonly string[] RequiredConfigurationKeys = { SecretKey, IssuerKey, AudienceKey };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ShellAccessFilter> _logger;

    public ShellAccessFilter(IConfiguration configuration, ILogger<ShellAccessFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Fails the host at startup when the JWT settings this filter validates tokens with are missing or empty,
    /// naming every key that is not set.
    /// </summary>
    /// <remarks>
    /// <para>This filter is GLOBAL (Program.cs) and is the only thing in Diten.Web that puts a VERIFIED principal
    /// on <c>HttpContext.User</c>. It used to answer missing configuration with a bare <c>return;</c>: token
    /// verification was skipped in full, <c>User</c> was left exactly as found, and nothing was written anywhere.
    /// The app happened to stay closed — no code path in Diten.Web calls <c>SignInAsync</c>, so <c>User</c> stayed
    /// anonymous and /Platform/* redirected to login — but that was an ACCIDENT of the surrounding code rather
    /// than a decision, and it would be noticed at the worst possible moment. A misconfigured deployment now
    /// refuses to boot instead, with the offending key named.</para>
    /// <para>⚠ Reads <see cref="IConfiguration"/>, deliberately — that is the same source
    /// <see cref="EnsureJwtCookiePrincipal"/> reads, so this is EXACTLY the filter's precondition rather than an
    /// approximation of it. It is not routed through <c>ValidateRequiredSecrets</c> (which already covers
    /// <c>JwtSettings:Secret</c> and would throw first): that validator resolves from environment variables ONLY
    /// in Production, and Issuer/Audience legitimately ship in appsettings.json, so requiring them there would
    /// break a correct deployment.</para>
    /// </remarks>
    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var missing = MissingConfigurationKeys(configuration);
        if (missing.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{nameof(ShellAccessFilter)} cannot verify access tokens because required configuration is missing " +
            $"or empty: {string.Join(", ", missing)}. Diten.Web will not start until every key is set.");
    }

    private static string[] MissingConfigurationKeys(IConfiguration configuration) =>
        RequiredConfigurationKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToArray();

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
                var loginPath = IsTenantScopedPlatformPath(path) ? "/account/login" : "/platform/login";
                context.Result = BuildLoginRedirect(loginPath, request);
                return;
            }

            if (IsTenantScopedPlatformPath(path) && string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Array.IndexOf(PlatformActors, actorType) < 0)
            {
                // Bare 403 → UseStatusCodePagesWithReExecute renders the friendly /Home/Status/403 page
                // (a ForbidResult would 302-redirect to the cookie scheme's AccessDenied path instead).
                context.Result = new StatusCodeResult(403);
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
            context.Result = new StatusCodeResult(403);
        }
    }

    private static bool IsReferenceDataPath(Microsoft.AspNetCore.Http.PathString path) =>
        path.StartsWithSegments(ReferenceDataPath, StringComparison.OrdinalIgnoreCase);

    // Platform-hosted screens that are nonetheless tenant-scoped and reachable by tenant_user actors.
    private static bool IsTenantScopedPlatformPath(Microsoft.AspNetCore.Http.PathString path) =>
        IsReferenceDataPath(path) ||
        path.StartsWithSegments(WorkflowPath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(PersonReferencesPath, StringComparison.OrdinalIgnoreCase);

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

        var missingConfiguration = MissingConfigurationKeys(_configuration);
        if (missingConfiguration.Length > 0)
        {
            /*
             * ⚠ FAIL LOUD AND CLOSED — never the silent `return;` that used to live here.
             *
             * ValidateConfiguration runs at startup, so a booted host has already proved these keys are set.
             * Reaching this branch means configuration was emptied UNDER a running process (appsettings.json is
             * registered with reloadOnChange). No verification is possible, so no principal is vouched for: the
             * request is explicitly anonymous, and the deployment problem is on the record with the key named.
             *
             * The cookies are NOT cleared. A server-side misconfiguration is not the visitor's fault, and
             * signing every session out over it would turn a config slip into a fleet-wide logout.
             */
            _logger.LogError(
                "ShellAccessFilter cannot verify the access token: required configuration is missing or empty ({MissingKeys}). Treating the request as anonymous.",
                string.Join(", ", missingConfiguration));

            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            return;
        }

        var jwtSecret = _configuration[SecretKey]!;
        var jwtIssuer = _configuration[IssuerKey]!;
        var jwtAudience = _configuration[AudienceKey]!;

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
                ClockSkew = JwtValidationDefaults.ClockSkew
            }, out _);

            context.User = principal;
        }
        catch (Exception ex)
        {
            // Warning, not Error: a rejected token is EXPECTED traffic on a public-facing shell — an expired
            // session, a token from another environment, a stale cookie. Only a broken deployment is an Error.
            _logger.LogWarning(
                ex,
                "Access-token validation failed ({ExceptionType}); clearing the auth cookies and treating the request as anonymous.",
                ex.GetType().Name);
            AuthTokenCookies.ClearTokens(context.Response);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
