using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Diten.ApiGateway.Middleware;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsNonTenantPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // /api/platform-auth/* (login, change-password/forced, forgot-password, reset-password)
        // is the platform identity surface. AuthService enforces its own authorization.
        // The tenant-resolution middleware must not gate it on tenant_user actor types.
        // See master-plan §1.3 (platform/admin paths bypass tenant resolution).
        if (IsPlatformAuthPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var actorType = ReadActorType(context.User);
        var host = context.Request.Host.Host;
        var isAdminHost = IsAdminHost(host);
        var isTenantHost = IsTenantHost(host);
        var isAuthLifecyclePath = IsAuthLifecyclePath(context.Request.Path);
        var jwtTenant = ReadJwtTenant(context.User) ?? ReadJwtTenantFromRequestToken(context);
        var headerTenant = ReadHeaderTenant(context.Request.Headers[TenantHeader]);
        var subdomainTenant = ReadSubdomainTenant(context.Request.Host.Host);

        if (isAdminHost && !IsAdminHostAllowedPath(context.Request.Path))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Host/Path Combination", "Admin host can only call platform management endpoints.");
            return;
        }

        if (isTenantHost && IsAdminPath(context.Request.Path))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Host/Path Combination", "Tenant hosts cannot access platform admin endpoints.");
            return;
        }

        if (IsPersonalizationPath(context.Request.Path))
        {
            if (IsPlatformActor(actorType))
            {
                if (context.Request.Headers.ContainsKey(TenantHeader))
                {
                    await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Invalid Tenant Header", $"'{TenantHeader}' is not allowed for platform personalization requests.");
                    return;
                }

                await _next(context);
                return;
            }

            var personalizationTenant = Resolve(jwtTenant, headerTenant, subdomainTenant, context);
            if (personalizationTenant is null && TryGetDevelopmentBypassTenant(out var personalizationBypassTenant))
            {
                personalizationTenant = personalizationBypassTenant;
                _logger.LogWarning(
                    "TenantResolution dev bypass applied in gateway. Path={Path} TenantId={TenantId}",
                    context.Request.Path,
                    personalizationBypassTenant);
            }

            if (personalizationTenant is null)
            {
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' or JWT tenant_id claim is required for tenant personalization endpoints.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(actorType) && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant personalization endpoints require tenant_user tokens.");
                return;
            }

            context.Request.Headers[TenantHeader] = personalizationTenant.Value.ToString();
            context.Items[TenantHeader] = personalizationTenant.Value;

            await _next(context);
            return;
        }

        if (IsAdminPath(context.Request.Path))
        {
            if (context.Request.Headers.ContainsKey(TenantHeader))
            {
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Invalid Tenant Header", $"'{TenantHeader}' is not allowed on admin endpoints.");
                return;
            }

            if (!IsPublicEndpoint(context.Request.Path) && !isAuthLifecyclePath && !IsPlatformActor(actorType))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Platform admin or partner admin token is required.");
                return;
            }

            await _next(context);
            return;
        }

        var resolvedTenant = Resolve(jwtTenant, headerTenant, subdomainTenant, context);
        if (resolvedTenant is null && TryGetDevelopmentBypassTenant(out var bypassTenant))
        {
            resolvedTenant = bypassTenant;
            _logger.LogWarning(
                "TenantResolution dev bypass applied in gateway. Path={Path} TenantId={TenantId}",
                context.Request.Path,
                bypassTenant);
        }

        if (resolvedTenant is null)
        {
            if (IsPublicEndpoint(context.Request.Path))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Missing Tenant",
                status = 400,
                detail = $"'{TenantHeader}' or JWT tenant_id claim is required for tenant endpoints.",
                traceId = context.TraceIdentifier
            });
            return;
        }

        if (!IsPublicEndpoint(context.Request.Path)
            && !isAuthLifecyclePath
            && !string.IsNullOrWhiteSpace(actorType)
            && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant endpoints require tenant_user tokens.");
            return;
        }

        context.Request.Headers[TenantHeader] = resolvedTenant.Value.ToString();
        context.Items[TenantHeader] = resolvedTenant.Value;

        await _next(context);
    }

    private Guid? Resolve(Guid? jwtTenant, Guid? headerTenant, Guid? subdomainTenant, HttpContext context)
    {
        if (jwtTenant.HasValue)
        {
            if (headerTenant.HasValue && headerTenant != jwtTenant)
            {
                _logger.LogWarning(
                    "Tenant conflict in gateway. JWT tenant wins. HeaderTenant={HeaderTenant}, JwtTenant={JwtTenant}, Path={Path}",
                    headerTenant,
                    jwtTenant,
                    context.Request.Path);
            }

            if (subdomainTenant.HasValue && subdomainTenant != jwtTenant)
            {
                _logger.LogWarning(
                    "Tenant conflict in gateway. JWT tenant wins over subdomain. SubdomainTenant={SubdomainTenant}, JwtTenant={JwtTenant}, Host={Host}",
                    subdomainTenant,
                    jwtTenant,
                    context.Request.Host.Host);
            }

            return jwtTenant;
        }

        if (headerTenant.HasValue)
        {
            return headerTenant;
        }

        return subdomainTenant;
    }

    private static Guid? ReadJwtTenant(ClaimsPrincipal user)
    {
        var raw = FindClaimValue(user, "tenant_id");
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string? ReadActorType(ClaimsPrincipal user)
    {
        return FindClaimValue(user, "actor_type");
    }

    private static Guid? ReadJwtTenantFromRequestToken(HttpContext context)
    {
        var raw = ReadClaimFromRequestToken(context, "tenant_id");
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static async Task EnsureAuthenticatedUserAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var result = await context.AuthenticateAsync("Bearer");
        if (result.Succeeded && result.Principal is not null)
        {
            context.User = result.Principal;
        }
    }

    private static string? FindClaimValue(ClaimsPrincipal user, string claimType)
    {
        return user.Claims
            .FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string? ReadClaimFromRequestToken(HttpContext context, string claimType)
    {
        var token = ReadBearerToken(context);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Claims
                .FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return context.Request.Cookies.TryGetValue("access_token", out var cookieToken)
            ? cookieToken
            : null;
    }

    private static Guid? ReadHeaderTenant(string? headerValue)
    {
        return Guid.TryParse(headerValue, out var parsed) ? parsed : null;
    }

    private static Guid? ReadSubdomainTenant(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        return Guid.TryParse(parts[0], out var parsed) ? parsed : null;
    }

    private static bool IsNonTenantPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/lookups", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/register", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthLifecyclePath(PathString path)
    {
        return path.StartsWithSegments("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminPath(PathString path)
    {
        return path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/platform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformAuthPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPersonalizationPath(PathString path)
    {
        return path.StartsWithSegments("/api/personalization", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminHostAllowedPath(PathString path)
    {
        return IsAdminPath(path)
               || IsPersonalizationPath(path)
               || path.StartsWithSegments("/api/platform-auth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminHost(string host)
    {
        return host.Equals("admin.diten.tech", StringComparison.OrdinalIgnoreCase)
               || host.Equals("admin.localhost", StringComparison.OrdinalIgnoreCase)
               || host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTenantHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.StartsWith("127.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsAdminHost(host);
    }

    private static async Task WriteProblemDetails(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        });
    }

    private bool TryGetDevelopmentBypassTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        if (!_environment.IsDevelopment())
        {
            return false;
        }

        if (!_configuration.GetValue<bool>("TenantResolution:DevBypassEnabled"))
        {
            return false;
        }

        var rawTenant = _configuration["TenantResolution:DevBypassTenantId"];
        if (!Guid.TryParse(rawTenant, out tenantId))
        {
            _logger.LogWarning(
                "TenantResolution dev bypass enabled but TenantId is invalid. ConfigValue={ConfigValue}",
                rawTenant);
            return false;
        }

        return true;
    }
}
