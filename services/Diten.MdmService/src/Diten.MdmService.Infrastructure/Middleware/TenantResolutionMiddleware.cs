using Diten.MdmService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Infrastructure.Middleware;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

        if (jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
        {
            _logger.LogWarning("Tenant mismatch detected for {Path}", context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Tenant mismatch", "JWT tenant and X-Tenant-Id must match.");
            return;
        }

        var resolvedTenant = jwtTenant ?? headerTenant;
        if (resolvedTenant is null)
        {
            _logger.LogWarning("Tenant context missing for {Path}", context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", "'X-Tenant-Id' header or JWT tenant_id claim is required.");
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
        => path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);

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
}
