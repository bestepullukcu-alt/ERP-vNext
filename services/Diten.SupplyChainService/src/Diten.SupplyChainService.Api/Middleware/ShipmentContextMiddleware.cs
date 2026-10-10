using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Api.Middleware;
public sealed class ShipmentContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, RequestContext context, ILegalEntityScopeValidator legalEntities)
    {
        if (HttpMethods.IsOptions(http.Request.Method)) { await next(http); return; }
        if (!http.Request.Path.StartsWithSegments("/api/shipment-bundle")) { await next(http); return; }
        var correlation = http.Request.Headers["X-Correlation-Id"];
        if (correlation.Count != 1 || !Guid.TryParse(correlation, out var correlationId) || correlationId == Guid.Empty)
        {
            // Q280: the generated trace id goes on the response header like a valid one does, so the log can carry it too.
            var generated = Guid.NewGuid(); http.Response.Headers["X-Correlation-Id"] = generated.ToString();
            await Error(http, 400, "A non-empty UUID X-Correlation-Id is required.", generated); return;
        }
        context.CorrelationId = correlationId;
        http.Response.Headers["X-Correlation-Id"] = correlationId.ToString();
        if (http.User.Identity?.IsAuthenticated != true) { await Error(http, 401, "Authentication required.", correlationId); return; }
        // R-2: the token no longer carries legal_entity_id, so only tenant and actor are read from it.
        var tenants = http.User.FindAll("tenant_id").ToArray(); var actors = http.User.FindAll("sub").ToArray();
        if (tenants.Length != 1 || actors.Length != 1
         || !Guid.TryParse(tenants[0].Value, out var tenant) || tenant == Guid.Empty
         || !Guid.TryParse(actors[0].Value, out var actor) || actor == Guid.Empty)
        { await Error(http, 403, "Trusted tenant and actor context required.", correlationId); return; }
        if (!ValidHeader(http, "X-Tenant-Id", out var headerTenant) || !ValidHeader(http, "X-Legal-Entity-Id", out var headerEntity))
        { await Error(http, 400, "Scope headers are required UUIDs.", correlationId); return; }
        if (tenant != headerTenant)
        { ShipmentTelemetry.ScopeDenials.Add(1); await Error(http, 404, "Shipment not found.", correlationId, ContractErrorCodes.ShipmentNotFound); return; }
        // R-2: LegalEntityId arrives with the request, so X-Legal-Entity-Id is its only source and MDM — not the
        // caller — decides whether it is the tenant's and Active. Every outcome but Valid refuses (fail-closed).
        var entity = headerEntity;
        switch (await legalEntities.ValidateAsync(tenant, entity, http.Request.Headers.Authorization.ToString(), correlationId, http.RequestAborted))
        {
            // MDM cannot tell a foreign id from an inactive one (TenantFilter is inside its lookup), so both
            // answer with the module's own NOT_FOUND — where the retired entity != headerEntity branch sent a
            // mismatch — and the response discloses nothing either way. The denial counter still fires.
            case LegalEntityScopeOutcome.NotReferenceable:
                ShipmentTelemetry.ScopeDenials.Add(1);
                await Error(http, 404, "Shipment not found.", correlationId, ContractErrorCodes.ShipmentNotFound); return;
            case LegalEntityScopeOutcome.Unavailable:
                await Error(http, 503, "Legal entity validation is unavailable.", correlationId, ContractErrorCodes.DependencyUnavailable); return;
        }
        if (http.Request.Query.Keys.Any(k => k.Equals("tenantId", StringComparison.OrdinalIgnoreCase) || k.Equals("legalEntityId", StringComparison.OrdinalIgnoreCase)))
        { await Error(http, 400, "Scope is not accepted from the query.", correlationId); return; }
        context.Scope = new ShipmentScope(tenant, entity, actor);
        context.Permissions = http.User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        if (HttpMethods.IsPost(http.Request.Method))
        {
            var key = http.Request.Headers["Idempotency-Key"];
            if (key.Count != 1 || string.IsNullOrWhiteSpace(key) || key.ToString().Length > 128)
            { await Error(http, 400, "Idempotency-Key must contain 1 to 128 characters.", correlationId); return; }
            context.IdempotencyKey = key.ToString().Trim();
        }
        await next(http);
    }
    private static bool ValidHeader(HttpContext http, string name, out Guid id)
    {
        id = Guid.Empty; return http.Request.Headers[name].Count == 1 && Guid.TryParse(http.Request.Headers[name], out id) && id != Guid.Empty;
    }
    private static Task Error(HttpContext http, int status, string message, Guid correlation, string code = "INVALID_REQUEST")
    {
        http.Response.StatusCode = status; return http.Response.WriteAsJsonAsync(ContractError.Create(code, message, correlation), http.RequestAborted);
    }
}
