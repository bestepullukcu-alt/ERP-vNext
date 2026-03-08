using Diten.AuthService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Infrastructure.Middleware;

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
            _logger.LogWarning("X-Tenant-Id header eksik. Path={Path}", context.Request.Path);
            await WriteProblemDetails(context, "Missing Tenant", $"'{TenantHeader}' header zorunludur.");
            return;
        }

        if (!Guid.TryParse(headerValue, out var tenantId))
        {
            _logger.LogWarning("X-Tenant-Id geçersiz GUID. Value={Value}", (string)headerValue!);
            await WriteProblemDetails(context, "Invalid Tenant", $"'{TenantHeader}' geçerli bir GUID olmalıdır.");
            return;
        }

        tenantContext.SetTenant(tenantId);
        _logger.LogDebug("TenantId çözümlendi: {TenantId}", tenantId);

        await _next(context);
    }

    private static async Task WriteProblemDetails(HttpContext context, string title, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = title,
            status = 400,
            detail = detail,
            traceId = context.TraceIdentifier
        });
    }
}
