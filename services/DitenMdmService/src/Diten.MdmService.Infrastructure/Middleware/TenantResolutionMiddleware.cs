using Diten.MdmService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Infrastructure.Middleware;

/// <summary>
/// X-Tenant-Id header'ını okur ve TenantContext'e set eder.
/// Header yoksa veya geçersiz GUID ise 400 ProblemDetails döner.
/// </summary>
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
        // Tenant gerektirmeyen durumlar
        var path = context.Request.Path;
        var method = context.Request.Method;

        // CORS Preflight (OPTIONS) istekleri tenant gerektirmez
        if (HttpMethods.IsOptions(method))
        {
            await _next(context);
            return;
        }

        // Health check tenant gerektirmez
        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Swagger UI tenant gerektirmez
        if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Browser favicon isteği tenant gerektirmez
        if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }


        if (!context.Request.Headers.TryGetValue(TenantHeader, out var headerValue)
            || string.IsNullOrWhiteSpace(headerValue))
        {
            _logger.LogWarning("X-Tenant-Id header eksik. Path={Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Missing Tenant",
                status = 400,
                detail = $"'{TenantHeader}' header zorunludur.",
                traceId = context.TraceIdentifier
            });
            return;
        }

        if (!Guid.TryParse(headerValue, out var tenantId))
        {
            _logger.LogWarning("X-Tenant-Id geçersiz GUID. Value={Value}", (string)headerValue!);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Invalid Tenant",
                status = 400,
                detail = $"'{TenantHeader}' geçerli bir GUID olmalıdır.",
                traceId = context.TraceIdentifier
            });
            return;
        }

        tenantContext.SetTenant(tenantId);

        _logger.LogDebug("TenantId çözümlendi: {TenantId}", tenantId);

        await _next(context);
    }
}
