using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.CrmService.Api.Filters;

/// <summary>
/// MOD-0149 §30.2, decision K-11 (owner, 2026-11-05): the CRM-ACCOUNT-LOOKUP endpoints (U-1, U-3) work ONLY with a user
/// token that carries <c>tenant_id</c>. Without it (or with a value that is not a GUID) the request is 400 and the action
/// never runs, even when an <c>X-Tenant-Id</c> header is present: the Gateway's token-less tenant paths (client header,
/// subdomain, Development fallback) cannot choose the tenant of these endpoints.
/// <para>⚠ PLACEMENT IS THE RULE. Put this attribute on the U-1 and U-3 ACTIONS only: never on a controller, never in
/// <c>MvcOptions.Filters</c>, never in the shared <c>TenantResolutionMiddleware</c>. Every other CRM endpoint keeps its
/// behaviour; whether a tenant-less token may use them is BL-324's decision, not this one. A test reads the endpoint
/// metadata and the global filter list to hold this.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireTokenTenantAttribute : Attribute, IActionFilter
{
    /// <summary>The claim CRM's shared tenant middleware reads (<c>TenantResolutionMiddleware.ReadJwtTenant</c>); a test
    /// compares the two so they cannot drift apart.</summary>
    public const string TenantClaimType = "tenant_id";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var claim = context.HttpContext.User.FindFirst(TenantClaimType)?.Value;
        if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "Tenant token required",
            Status = StatusCodes.Status400BadRequest,
            Detail = $"This endpoint requires a token that carries '{TenantClaimType}'."
        })
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
