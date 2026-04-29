using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Common.Tenancy;

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

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsBypassPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var actorType = context.User.FindFirst("actor_type")?.Value;
        if (IsPersonalizationPath(context.Request.Path))
        {
            if (IsPlatformActor(actorType))
            {
                if (context.Request.Headers.ContainsKey(TenantHeader))
                {
                    await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Invalid Tenant Header", $"'{TenantHeader}' is not allowed for platform personalization requests.");
                    return;
                }

                tenantContext.SetPlatformContext(Guid.Empty);
                await _next(context);
                return;
            }

            var personalizationJwtTenant = ReadJwtTenant(context);
            var personalizationHeaderTenant = ReadHeaderTenant(context);
            var personalizationTenant = ResolveTenant(personalizationJwtTenant, personalizationHeaderTenant, context);
            if (personalizationTenant is null && TryGetDevelopmentBypassTenant(out var personalizationBypassTenant))
            {
                personalizationTenant = personalizationBypassTenant;
                _logger.LogWarning(
                    "TenantResolution dev bypass applied. Path={Path} TenantId={TenantId}",
                    context.Request.Path,
                    personalizationBypassTenant);
            }

            if (personalizationTenant is null)
            {
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' header or JWT tenant_id claim is required for tenant personalization endpoints.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(actorType) && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant personalization endpoints require tenant_user tokens.");
                return;
            }

            tenantContext.SetTenant(personalizationTenant.Value);
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

            if (!IsPlatformActor(actorType))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Platform admin or partner admin token is required.");
                return;
            }

            tenantContext.SetPlatformContext(Guid.Empty);
            await _next(context);
            return;
        }

        var jwtTenant = ReadJwtTenant(context);
        var headerTenant = ReadHeaderTenant(context);
        var resolvedTenant = ResolveTenant(jwtTenant, headerTenant, context);
        if (resolvedTenant is null && TryGetDevelopmentBypassTenant(out var bypassTenant))
        {
            resolvedTenant = bypassTenant;
            _logger.LogWarning(
                "TenantResolution dev bypass applied. Path={Path} TenantId={TenantId}",
                context.Request.Path,
                bypassTenant);
        }

        if (resolvedTenant is null)
        {
            if (IsPublicAuthPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            _logger.LogWarning("Tenant context missing. Path={Path}", context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' header or JWT tenant_id claim is required.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(actorType) && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant endpoints require tenant_user tokens.");
            return;
        }

        tenantContext.SetTenant(resolvedTenant.Value);
        await _next(context);
    }

    private Guid? ResolveTenant(Guid? jwtTenant, Guid? headerTenant, HttpContext context)
    {
        if (jwtTenant.HasValue)
        {
            if (headerTenant.HasValue && headerTenant != jwtTenant)
            {
                _logger.LogWarning(
                    "Tenant conflict. JWT tenant wins. HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
                    headerTenant,
                    jwtTenant,
                    context.Request.Path);
            }
            return jwtTenant;
        }
        return headerTenant;
    }

    private static Guid? ReadJwtTenant(HttpContext context)
    {
        var claimValue = context.User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : null;
    }

    private static Guid? ReadHeaderTenant(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(TenantHeader, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }
        return Guid.TryParse(headerValue, out var tenantId) ? tenantId : null;
    }

    private static bool IsBypassPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/lookups", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicAuthPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth/login", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminPath(PathString path)
    {
        return path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/platform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPersonalizationPath(PathString path)
    {
        return path.StartsWithSegments("/api/personalization", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteProblemDetails(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        });
        await context.Response.WriteAsync(json);
    }

    private bool TryGetDevelopmentBypassTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        if (!_environment.IsDevelopment())
        {
            return false;
        }

        var bypassEnabledRaw = _configuration["TenantResolution:DevBypassEnabled"];
        if (!bool.TryParse(bypassEnabledRaw, out var bypassEnabled) || !bypassEnabled)
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
