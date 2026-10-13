using Diten.ManufacturingService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.ManufacturingService.Infrastructure.Middleware;

/// <summary>
/// Every BOM request runs in a legal entity the caller chose (TenantResolutionMiddleware). This proves, BEFORE any
/// handler runs, that the id belongs to the token's tenant and is ACTIVE — asking MDM each time, no cache (a cached
/// "yes" would let a deactivated legal entity keep writing). Fail-closed:
/// <list type="bullet">
/// <item>MDM says no (foreign tenant, unknown, suspended, not referenceable) → 422 <c>LEGAL_ENTITY_NOT_REFERENCEABLE</c>;</item>
/// <item>MDM cannot be asked → 503 <c>DEPENDENCY_UNAVAILABLE</c>, nothing read or written.</item>
/// </list>
/// Runs after authentication (the caller's token is forwarded to MDM) and only for requests the tenant middleware
/// resolved; health and OPTIONS are not scoped.
/// </summary>
public sealed class LegalEntityValidationMiddleware(RequestDelegate next, ILogger<LegalEntityValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, ILegalEntityReferenceValidator validator, ICorrelationContext correlation)
    {
        if (!tenant.IsResolved || context.User.Identity?.IsAuthenticated != true)
        {
            // Unscoped paths (health) and unauthenticated calls: authorization answers them; no MDM call on behalf of nobody.
            await next(context);
            return;
        }

        var result = await validator.ValidateAsync(tenant.LegalEntityId, context.RequestAborted);
        if (result == LegalEntityValidation.Valid)
        {
            await next(context);
            return;
        }

        var (status, code, message) = result == LegalEntityValidation.NotReferenceable
            ? (StatusCodes.Status422UnprocessableEntity, BomErrorCodes.LegalEntityNotReferenceable, BomErrorCodes.Message(BomErrorCodes.LegalEntityNotReferenceable))
            : (StatusCodes.Status503ServiceUnavailable, BomErrorCodes.DependencyUnavailable, BomErrorCodes.Message(BomErrorCodes.DependencyUnavailable));
        logger.LogWarning("Legal entity {LegalEntityId} refused ({Result}) Path={Path}", tenant.LegalEntityId, result, context.Request.Path);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            error = new { code, message, correlationId = correlation.CorrelationId },
            contractVersion = "v1"
        });
    }
}
