using System.Security.Claims;
using Diten.WebAPI.Security;
using Microsoft.AspNetCore.Authorization;

namespace Diten.WebAPI.Middleware;

public sealed class EsbpSecurityPerimeterMiddleware
{
    public const string TenantHeader = "X-Tenant-Id";
    public const string TenantClaim = "tenant_id";
    public const string ActorClaim = "sub";
    public const string ActorTypeClaim = "actor_type";
    public const string PermissionClaim = "permission";
    public const string TenantSafetyCode = "TENANT_SAFETY_NOT_READY";

    private readonly RequestDelegate _next;

    public EsbpSecurityPerimeterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, EsbpTenantContext tenantContext)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var tenantClaim = context.User.FindFirstValue(TenantClaim);
        var actorId = context.User.FindFirstValue(ActorClaim);
        var actorType = context.User.FindFirstValue(ActorTypeClaim);
        var headerValue = context.Request.Headers[TenantHeader].ToString();

        if (!Guid.TryParse(tenantClaim, out var claimedTenant) ||
            !Guid.TryParse(headerValue, out var headerTenant))
        {
            await WriteFailureAsync(context, StatusCodes.Status400BadRequest, "INVALID_TENANT_CONTEXT");
            return;
        }

        if (claimedTenant != headerTenant)
        {
            await WriteFailureAsync(context, StatusCodes.Status400BadRequest, "TENANT_CONTEXT_MISMATCH");
            return;
        }

        if (string.IsNullOrWhiteSpace(actorId) || !string.Equals(actorType, "tenant_user", StringComparison.Ordinal))
        {
            await WriteFailureAsync(context, StatusCodes.Status403Forbidden, "INVALID_ACTOR_CONTEXT");
            return;
        }

        tenantContext.Resolve(claimedTenant, actorId);

        var requiredPermission = GetRequiredPermission(endpoint);
        if (requiredPermission is null || !HasExactPermission(context.User, requiredPermission))
        {
            await WriteFailureAsync(context, StatusCodes.Status403Forbidden, "FORBIDDEN");
            return;
        }

        // Existing ES entities and repositories carry no authoritative TenantId. Authentication and
        // request binding therefore cannot make a business query tenant-safe. The bounded perimeter stays
        // default-off until the separately governed schema/data migration proves ownership.
        await WriteFailureAsync(context, StatusCodes.Status503ServiceUnavailable, TenantSafetyCode);
    }

    private static string? GetRequiredPermission(Endpoint endpoint)
    {
        var strategy = endpoint.Metadata.GetMetadata<EnterpriseStrategyPermissionAttribute>();
        if (strategy is not null)
        {
            return strategy.Permission;
        }

        return endpoint.Metadata.GetMetadata<DeliveryExecutionManagementPermissionAttribute>()?.Permission;
    }

    private static bool HasExactPermission(ClaimsPrincipal user, string permission)
        => user.Claims.Any(claim =>
            string.Equals(claim.Type, PermissionClaim, StringComparison.Ordinal) &&
            string.Equals(claim.Value, permission, StringComparison.Ordinal));

    private static async Task WriteFailureAsync(HttpContext context, int statusCode, string code)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            code,
            correlationId = context.TraceIdentifier
        }, context.RequestAborted);
    }
}
