namespace Diten.PlanningService.Api.Features.DemandPlanning;

public sealed class DemandTenantMiddleware
{
    private readonly RequestDelegate _next;

    public DemandTenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Authorization handles unauthenticated callers. No tenant context is taken from a body or query.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers["X-Tenant-Id"].ToString();
        var claim = context.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(header, out var headerTenant) || headerTenant == Guid.Empty ||
            !Guid.TryParse(claim, out var claimTenant) || claimTenant == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (headerTenant != claimTenant)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Items["DemandPlanning.TenantId"] = claimTenant;
        await _next(context);
    }
}
