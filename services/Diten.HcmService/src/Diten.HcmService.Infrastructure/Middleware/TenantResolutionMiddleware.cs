using Diten.HcmService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.HcmService.Infrastructure.Middleware;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var jwtTenant = ReadJwtTenant(context);
        var headerTenant = ReadHeaderTenant(context);

        // BL-324 case 1 — the header and the token NAME DIFFERENT TENANTS: refused 400 (DCP-004 §7.4, owner
        // decision 2026-08-29). A malformed request, not an access decision — the caller wrote both values, so
        // nothing is concealed by saying so.
        //
        // ⚠ WHAT THIS CLOSED. This middleware never read the JWT at all: the header alone named the tenant, so an
        // authenticated user of tenant A could send `X-Tenant-Id: B` and this service would act for B. The service
        // authenticates (AddAuthentication/UseAuthentication, [Authorize] on every controller) and the middleware
        // runs AFTER UseAuthentication, so the token's tenant was available and simply never consulted.
        // [Authorize] proves WHO you are, not WHICH TENANT you may act for.
        //
        // ⚠ The JWT is read here to DETECT the contradiction, deliberately not to resolve the tenant. Resolution
        // stays header-driven exactly as before, including the Clear() below when the header is absent: whether the
        // JWT should also become a tenant SOURCE here is a separate trust decision (BL-324), not this refusal.
        if (jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
        {
            _logger.LogWarning(
                "Tenant mismatch in HcmService. HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
                headerTenant,
                jwtTenant,
                context.Request.Path);
            await WriteProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Tenant mismatch",
                $"JWT tenant and '{TenantHeaderName}' must match.");
            return;
        }

        if (headerTenant.HasValue)
        {
            tenantContext.SetTenant(headerTenant.Value);
        }
        else
        {
            tenantContext.Clear();
        }

        await _next(context);
    }

    private static Guid? ReadJwtTenant(HttpContext context)
    {
        var claimValue = context.User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : null;
    }

    private static Guid? ReadHeaderTenant(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValue))
        {
            return null;
        }

        return Guid.TryParse(headerValue.FirstOrDefault(), out var tenantId) ? tenantId : null;
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
}
