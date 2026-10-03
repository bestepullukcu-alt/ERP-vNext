using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.Platform.API.Security;

/// <summary>
/// BL-521 — THE ONLY WAY A PARTNER ADMINISTRATOR REACHES A PLATFORM ENDPOINT. Without this attribute on the endpoint
/// (action or controller) a <c>partner_admin</c> is refused 403 by both platform gates (the <c>PlatformActor</c>
/// policy and <see cref="HasPermissionAttribute"/>): default-deny, so an endpoint added tomorrow is closed to partners
/// until someone decides it is tenant-scoped.
///
/// <para>With <see cref="TenantRouteKey"/> the endpoint addresses ONE tenant by that route value, and the request's
/// <see cref="IPlatformActorTenantScope"/> decides: a tenant outside the actor's scope is answered <b>404, as if it
/// did not exist</b> (CT decision: no existence leak), nothing runs, and the attempt is recorded as a security event.
/// Without it the endpoint is a list or a partner-owned object and filters by the scope itself.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PartnerAdminScopeAttribute : Attribute, IAsyncActionFilter, IOrderedFilter, IPartnerAdminAllowed
{
    /// <summary>First of the action filters: the tenant is out of scope before a body is validated or read.</summary>
    public int Order => int.MinValue;

    public const string OutOfScopeRequestType = "PartnerTenantScopeViolation";

    public PartnerAdminScopeAttribute(string? tenantRouteKey = null)
    {
        TenantRouteKey = tenantRouteKey;
    }

    /// <summary>The route value naming the tenant the endpoint addresses; null for lists and partner-owned objects.</summary>
    public string? TenantRouteKey { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (TenantRouteKey is null
            || !context.RouteData.Values.TryGetValue(TenantRouteKey, out var raw)
            || !Guid.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out var tenantId))
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var scope = await services.GetRequiredService<IPlatformActorTenantScope>().GetAsync(context.HttpContext.RequestAborted);
        if (scope.Allows(tenantId))
        {
            await next();
            return;
        }

        await RecordAsync(context, tenantId);
        context.Result = NotFound(context);
    }

    /// <summary>The answer for a tenant outside the scope: the same 404 a missing tenant gets.</summary>
    public static ObjectResult NotFound(ActionContext context)
    {
        var correlation = context.HttpContext.RequestServices.GetService<ICorrelationContext>()?.CorrelationId;
        return new ObjectResult(Response<NoContent>.Fail("Tenant not found.", StatusCodes.Status404NotFound, null,
            string.IsNullOrWhiteSpace(correlation) ? context.HttpContext.TraceIdentifier : correlation))
        {
            StatusCode = StatusCodes.Status404NotFound
        };
    }

    /// <summary>
    /// The out-of-scope attempt as a SECURITY audit event (central door): who, which tenant, which endpoint. Best
    /// effort — the request is refused whether or not the record could be written.
    /// </summary>
    private static async Task RecordAsync(ActionExecutingContext context, Guid tenantId)
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
                ActorType = AuditActorType.PartnerAdministrator,
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
