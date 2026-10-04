using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.Platform.API.Security;

/// <summary>
/// BL-521 — THE ONLY WAY A PARTNER ADMINISTRATOR REACHES A PLATFORM ENDPOINT. Without this attribute on the endpoint
/// (action or controller) a partner administrator is refused 403 by both platform gates (the <c>PlatformActor</c>
/// policy and <see cref="HasPermissionAttribute"/>): default-deny, so an endpoint added tomorrow is closed to partners
/// until someone decides it is tenant-scoped.
///
/// <para>What the attribute declares, and what a partner gets (a platform administrator — confirmed by its record — is
/// never affected by this filter):</para>
/// <list type="bullet">
/// <item><b>A tenant route key</b> (<c>[PartnerAdminScope("id")]</c>): the endpoint addresses ONE tenant by that route
/// value, and the request's <see cref="IPlatformActorTenantScope"/> decides. A tenant outside the scope is answered
/// <b>404, exactly as a missing tenant is</b> (CT decision: no existence leak), nothing runs, and the attempt is
/// recorded as a security event. FIX1 F2 — so is a declared key the route does not carry or that is not a tenant id:
/// an action without the key added later under a controller-level key is CLOSED to partners, not open.</item>
/// <item><see cref="FiltersInQuery"/>: a list or count over tenants; the handler applies the scope in its query. Must be
/// declared on the action itself, so it is a decision, not an accident of the controller.</item>
/// <item><see cref="NoTenant"/>: the endpoint carries no tenant data (reference values, the caller's own
/// account).</item>
/// <item>No argument: nothing decided → closed to partners (404, recorded), like a missing key.</item>
/// </list>
/// <para>The most specific declaration wins: an action's own attribute overrides its controller's.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PartnerAdminScopeAttribute : Attribute, IAsyncActionFilter, IOrderedFilter, IPartnerAdminAllowed
{
    /// <summary>
    /// First of the action filters. FIX1 F9 — action filters run AFTER model binding, so the request body has been read
    /// and bound by then; what this guarantees is that nothing of the action (handler, command, write) runs for a tenant
    /// outside the scope.
    /// </summary>
    public int Order => int.MinValue;

    public const string OutOfScopeRequestType = "PartnerTenantScopeViolation";

    /// <summary>A list or count over tenants that filters by the actor's scope in its own query.</summary>
    public const string FiltersInQuery = "@filters-in-query";

    /// <summary>The endpoint carries no tenant data at all (reference values, the caller's own account).</summary>
    public const string NoTenant = "@no-tenant";

    /// <summary>The 404 answer — the SAME body the tenant endpoints give a tenant that does not exist.</summary>
    public const string NotFoundMessage = "Tenant not found.";

    public PartnerAdminScopeAttribute(string? tenantRouteKey = null)
    {
        TenantRouteKey = tenantRouteKey;
    }

    /// <summary>The route value naming the tenant the endpoint addresses, <see cref="FiltersInQuery"/>,
    /// <see cref="NoTenant"/>, or null (undecided: closed to partners).</summary>
    public string? TenantRouteKey { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // An action's own declaration overrides its controller's: only the most specific instance decides.
        if (!context.IsEffectivePolicy<PartnerAdminScopeAttribute>(this))
        {
            await next();
            return;
        }

        if (TenantRouteKey is FiltersInQuery or NoTenant)
        {
            await next();
            return;
        }

        var scope = await context.HttpContext.RequestServices.GetRequiredService<IPlatformActorTenantScope>()
            .GetAsync(context.HttpContext.RequestAborted);

        Guid? tenantId = TenantRouteKey is not null
            && context.RouteData.Values.TryGetValue(TenantRouteKey, out var raw)
            && Guid.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out var parsed)
                ? parsed
                : null;

        // A platform administrator is not affected; a partner reaches only a tenant its scope allows.
        if (scope.IsUnrestricted || (tenantId is { } id && scope.Allows(id)))
        {
            await next();
            return;
        }

        await RecordAsync(context, scope, tenantId);
        context.Result = NotFound(context);
    }

    /// <summary>
    /// The answer for a tenant outside the scope (or no tenant at all): byte for byte the 404 a missing tenant gets from
    /// the tenant endpoints — <c>data: null</c>, no reason code, no correlation id — so the answer cannot tell "exists,
    /// not yours" from "does not exist".
    /// </summary>
    public static ObjectResult NotFound(ActionContext context) =>
        new(Response<object>.Fail(NotFoundMessage, StatusCodes.Status404NotFound))
        {
            StatusCode = StatusCodes.Status404NotFound
        };

    /// <summary>
    /// The out-of-scope attempt as a SECURITY audit event (central door): who, which tenant, which endpoint. Best
    /// effort — the request is refused whether or not the record could be written.
    /// </summary>
    private static async Task RecordAsync(ActionExecutingContext context, PlatformTenantScope scope, Guid? tenantId)
    {
        try
        {
            var audit = context.HttpContext.RequestServices.GetService<IAuditService>();
            if (audit is null) return;
            var principal = context.HttpContext.User;
            var userId = Guid.TryParse(principal.FindFirst("sub")?.Value ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
                ? id : (Guid?)null;
            await audit.AppendAsync(new AuditAppendRequest
            {
                CorrelationId = Guid.NewGuid(),
                RequestType = OutOfScopeRequestType,
                // A partner by its record or its token; otherwise a platform actor its record does not confirm.
                ActorType = scope.IsPartner || PlatformActorTenantScope.IsPartnerAdmin(principal)
                    ? AuditActorType.PartnerAdministrator
                    : AuditActorType.PlatformAdministrator,
                ActorId = userId,
                Category = AuditCategory.Security,
                EntityType = "Tenant",
                EntityId = tenantId,
                TargetTenantId = tenantId,
                Operation = AuditOperation.PermissionDenied,
                Outcome = AuditOutcome.Denied,
                SourceService = "Diten.Platform",
                Metadata = new Dictionary<string, object?>
                {
                    ["Endpoint"] = $"{context.HttpContext.Request.Method} {context.ActionDescriptor.AttributeRouteInfo?.Template}"
                },
                // A platform security event (filed under the platform), naming the tenant it was aimed at: the
                // tenant's own trail does not learn that a partner probed it.
                IsPlatformGlobal = true
            }, context.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The refusal stands without its record.
        }
    }
}
