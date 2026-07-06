using Diten.AuthService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Infrastructure.Middleware;

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

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsBypassPath(context.Request.Path))
        {
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
                "TenantResolution dev bypass applied in AuthService. Path={Path} TenantId={TenantId}",
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
                    "Tenant conflict in AuthService. JWT tenant wins. HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
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
               // All /internal/* endpoints are tenant-agnostic S2S surfaces (authenticated by the internal API key,
               // not a tenant JWT) — e.g. /internal/events and /internal/permissions/sync (catalog permission sync).
               // Requiring an X-Tenant-Id here silently broke module permission sync (400 Missing Tenant).
               || path.StartsWithSegments("/internal", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicAuthPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/mfa/verify", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/mfa/resend", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/register", StringComparison.OrdinalIgnoreCase)
               // Anonymous invitation redemption: no tenant header/JWT — the user is resolved by token hash.
               || path.StartsWithSegments("/api/users/set-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase);
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
