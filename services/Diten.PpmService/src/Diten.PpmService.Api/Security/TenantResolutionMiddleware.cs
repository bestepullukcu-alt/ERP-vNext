using System.Security.Claims;

namespace Diten.PpmService.Api.Security;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string TenantClaim = "tenant_id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/swagger"))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var claimValue = context.User.FindFirstValue(TenantClaim);
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            await WriteProblemDetails(
                context,
                "Missing Tenant",
                "The authenticated token must contain a tenant_id claim.");
            return;
        }

        if (!Guid.TryParse(claimValue, out var jwtTenant) || jwtTenant == Guid.Empty)
        {
            await WriteProblemDetails(
                context,
                "Invalid Tenant Identity Format",
                "The authenticated token contains an invalid tenant_id claim.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeader, out var headerValues))
        {
            await next(context);
            return;
        }

        var headerValue = headerValues.FirstOrDefault();
        if (!Guid.TryParse(headerValue, out var headerTenant) || headerTenant == Guid.Empty)
        {
            await WriteProblemDetails(
                context,
                "Invalid Tenant Identity Format",
                $"'{TenantHeader}' contains an invalid tenant identity.");
            return;
        }

        if (jwtTenant != headerTenant)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    title = "Tenant mismatch",
                    status = StatusCodes.Status400BadRequest,
                    detail = $"'{TenantHeader}' conflicts with the authenticated tenant.",
                    traceId = context.TraceIdentifier,
                    conflictingSignals = new[] { "header" }
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static Task WriteProblemDetails(HttpContext context, string title, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return context.Response.WriteAsJsonAsync(
            new
            {
                title,
                status = StatusCodes.Status400BadRequest,
                detail,
                traceId = context.TraceIdentifier
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
