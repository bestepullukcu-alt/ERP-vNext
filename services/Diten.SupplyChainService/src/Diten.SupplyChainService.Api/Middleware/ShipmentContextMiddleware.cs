using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Api.Middleware;
public sealed class ShipmentContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, RequestContext context)
    {
        if (HttpMethods.IsOptions(http.Request.Method)) { await next(http); return; }
        if (!http.Request.Path.StartsWithSegments("/api/shipment-bundle")) { await next(http); return; }
        var correlation = http.Request.Headers["X-Correlation-Id"];
        if (correlation.Count != 1 || !Guid.TryParse(correlation, out var correlationId) || correlationId == Guid.Empty)
        { await Error(http, 400, "A non-empty UUID X-Correlation-Id is required.", Guid.NewGuid()); return; }
        context.CorrelationId = correlationId;
        http.Response.Headers["X-Correlation-Id"] = correlationId.ToString();
        if (http.User.Identity?.IsAuthenticated != true) { await Error(http, 401, "Authentication required.", correlationId); return; }
        var tenants = http.User.FindAll("tenant_id").ToArray(); var entities = http.User.FindAll("legal_entity_id").ToArray(); var actors = http.User.FindAll("sub").ToArray();
        if (tenants.Length != 1 || entities.Length != 1 || actors.Length != 1
         || !Guid.TryParse(tenants[0].Value, out var tenant) || tenant == Guid.Empty
         || !Guid.TryParse(entities[0].Value, out var entity) || entity == Guid.Empty
         || !Guid.TryParse(actors[0].Value, out var actor) || actor == Guid.Empty)
        { await Error(http, 403, "Trusted tenant, legal entity and actor context required.", correlationId); return; }
        if (!ValidHeader(http, "X-Tenant-Id", out var headerTenant) || !ValidHeader(http, "X-Legal-Entity-Id", out var headerEntity))
        { await Error(http, 400, "Scope headers are required UUIDs.", correlationId); return; }
        if (tenant != headerTenant || entity != headerEntity) { await Error(http, 404, "Shipment not found.", correlationId, "SHIPMENT_NOT_FOUND"); return; }
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
