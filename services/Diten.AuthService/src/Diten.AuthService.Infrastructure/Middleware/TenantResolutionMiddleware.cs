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

        // BL-324 case 1 — the header and the token NAME DIFFERENT TENANTS. That is a malformed request, not an
        // access decision, so it is refused 400 and nothing is concealed: the caller wrote both values.
        //
        // ⚠ This used to let the JWT win with a warning (see the deleted ResolveTenant). A warning is not a
        // refusal: the request still ran, and which of the two contradicting values a handler downstream read
        // was silent. The rule is DCP-004 §7.4 (owner decision 2026-08-29, BL-323).
        //
        // A contradiction needs BOTH values, so this cannot fire on the public auth paths below — at login there
        // is no bearer token yet, hence no JWT tenant, hence nothing to contradict.
        if (jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
        {
            _logger.LogWarning(
                "Tenant mismatch in AuthService. HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
                headerTenant,
                jwtTenant,
                context.Request.Path);
            await WriteProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Tenant mismatch",
                $"JWT tenant and '{TenantHeader}' must match.");
            return;
        }

        var resolvedTenant = jwtTenant ?? headerTenant;
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
        // ⚠ contentType PASSED IN, never assigned to Response.ContentType beforehand. WriteAsJsonAsync sets
        // Response.ContentType UNCONDITIONALLY — an earlier assignment is overwritten, which is exactly how
        // this helper spent its whole life declaring problem+json and answering "application/json;
        // charset=utf-8" on the wire. The declaration only reaches the caller through this parameter.
        await context.Response.WriteAsJsonAsync(
            new
            {
                title,
                status = statusCode,
                detail,
                traceId = context.TraceIdentifier
            },
            options: null,
            contentType: "application/problem+json");
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
