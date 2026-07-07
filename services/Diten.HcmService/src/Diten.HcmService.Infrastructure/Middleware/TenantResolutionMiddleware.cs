using Diten.HcmService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.HcmService.Infrastructure.Middleware;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var values)
            && Guid.TryParse(values.FirstOrDefault(), out var tenantId))
        {
            tenantContext.SetTenant(tenantId);
        }
        else
        {
            tenantContext.Clear();
        }

        await _next(context);
    }
}
