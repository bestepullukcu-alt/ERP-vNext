using System.Security.Claims;

namespace Diten.PpmService.Api.Security;

public sealed class TenantHeaderConsistencyMiddleware(RequestDelegate next)
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string TenantClaim = "tenant_id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.Request.Headers.TryGetValue(TenantHeader, out var headerValues))
        {
            var claimValue = context.User.FindFirstValue(TenantClaim);
            var headerValue = headerValues.FirstOrDefault();

            if (!Guid.TryParse(claimValue, out var claimTenant)
                || !Guid.TryParse(headerValue, out var headerTenant)
                || claimTenant == Guid.Empty
                || headerTenant == Guid.Empty
                || claimTenant != headerTenant)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    statusCode = StatusCodes.Status400BadRequest,
                    isSuccessful = false,
                    errors = new[] { "Tenant context is inconsistent." },
                    reason_code = "tenant_context_mismatch"
                }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
