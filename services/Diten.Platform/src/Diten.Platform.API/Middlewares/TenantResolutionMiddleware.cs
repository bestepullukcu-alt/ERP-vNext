using Diten.Platform.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.API.Middlewares;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;

        if (HttpMethods.IsOptions(method))
        {
            await _next(context);
            return;
        }

        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeader, out var headerValue) ||
            string.IsNullOrWhiteSpace(headerValue))
        {
            _logger.LogWarning("X-Tenant-Id header is missing. Path={Path}", context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' header is required.");
            return;
        }

        if (!Guid.TryParse(headerValue, out var tenantId))
        {
            _logger.LogWarning("X-Tenant-Id header is not a valid GUID. Value={Value}", (string)headerValue!);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Invalid Tenant", $"'{TenantHeader}' must be a valid GUID.");
            return;
        }

        var authenticatedTenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(authenticatedTenantClaim, out var claimTenantId) &&
            claimTenantId != tenantId)
        {
            _logger.LogWarning("Tenant mismatch. HeaderTenant={HeaderTenant} ClaimTenant={ClaimTenant}", tenantId, claimTenantId);
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Tenant Mismatch", "JWT tenant claim does not match X-Tenant-Id.");
            return;
        }

        tenantContext.SetTenant(tenantId);
        await _next(context);
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
