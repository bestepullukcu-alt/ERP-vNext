using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
namespace Diten.SupplyChainService.Api.Features.Claims;
public sealed class ClaimContextMiddleware(RequestDelegate next)
{
    public static bool IsClaimPath(HttpContext http) => http.Request.Path.StartsWithSegments("/api/shipment-bundle/claims", StringComparison.OrdinalIgnoreCase);
    private static bool Uuid(string? text, out Guid value)
    {
        value = Guid.Empty;
        return ClaimWire.Uuid(text) && Guid.TryParseExact(text, "D", out value);
    }
    private static bool Header(HttpRequest request, string name, out Guid value)
    {
        value = Guid.Empty;
        return request.Headers.TryGetValue(name, out var values) && values.Count == 1 && Uuid(values[0], out value);
    }
    private static bool Identity(HttpContext http, string name, out Guid value)
    {
        value = Guid.Empty;
        var claims = http.User.FindAll(name).ToArray();
        return claims.Length == 1 && Uuid(claims[0].Value, out value) && value != Guid.Empty;
    }
    private static bool UniqueSignedIdentity(string authorization)
    {
        // Authentication must already have accepted the signature. This inspection
        // catches duplicates a parser may have collapsed; it is not authentication.
        try
        {
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
            var segment = authorization[7..].Split('.')[1].Replace('-', '+').Replace('_', '/');
            segment = segment.PadRight((segment.Length + 3) / 4 * 4, '=');
            using var payload = JsonDocument.Parse(Convert.FromBase64String(segment));
            return payload.RootElement.ValueKind == JsonValueKind.Object && new[] { "tenant_id", "sub" }.All(name =>
                payload.RootElement.EnumerateObject().Count(p => p.NameEquals(name)) == 1 &&
                payload.RootElement.GetProperty(name).ValueKind == JsonValueKind.String &&
                Uuid(payload.RootElement.GetProperty(name).GetString(), out var id) && id != Guid.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IndexOutOfRangeException or ArgumentException) { return false; }
    }
    public async Task InvokeAsync(HttpContext http, ClaimRequestContext context, RequestContext loggingContext, ILegalEntityScopeValidator legalEntities)
    {
        var permission = http.GetEndpoint()?.Metadata.GetMetadata<ClaimPermissionAttribute>();
        if (permission is null) { await next(http); return; }
        var validCorrelation = Header(http.Request, "X-Correlation-Id", out var correlation);
        context.CorrelationId = validCorrelation ? correlation : Guid.NewGuid();
        http.Response.Headers["X-Correlation-Id"] = context.CorrelationId.ToString();
        async Task Error(int status, string code)
        {
            http.Response.StatusCode = status;
            if (status == 401) http.Response.Headers.WWWAuthenticate = "Bearer";
            await http.Response.WriteAsJsonAsync(ClaimContractError.Create(code, status, context.CorrelationId));
        }
        var authorization = http.Request.Headers.Authorization;
        if (http.User.Identity?.IsAuthenticated != true || authorization.Count != 1) { await Error(401, "UNAUTHENTICATED"); return; }
        if (!UniqueSignedIdentity(authorization[0]!) || !Identity(http, "tenant_id", out var tenant) ||
            !Identity(http, "sub", out var actor) ||
            !permission.Permissions.Any(p => http.User.HasClaim("permission", p))) { await Error(403, "FORBIDDEN"); return; }
        if (!validCorrelation) { await Error(400, "INVALID_REQUEST"); return; }
        Guid headerTenant = tenant;
        if (http.Request.Headers.ContainsKey("X-Tenant-Id") && !Header(http.Request, "X-Tenant-Id", out headerTenant))
        { await Error(400, "INVALID_REQUEST"); return; }
        // R-2: X-Legal-Entity-Id was optional here because it defaulted to the claim. With the claim gone there is
        // nothing to default to, so it is REQUIRED — Claims is the only one of the five where this changed.
        if (!Header(http.Request, "X-Legal-Entity-Id", out var headerLe)) { await Error(400, "INVALID_REQUEST"); return; }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            var values = http.Request.Headers["Idempotency-Key"];
            if (values.Count != 1 || values[0] is not { } key || key.EnumerateRunes().Count() is < 1 or > 128)
            { await Error(400, "INVALID_REQUEST"); return; }
            context.IdempotencyKey = key;
        }
        if (headerTenant != tenant) { await Error(403, "FORBIDDEN"); return; }
        // R-2: the request carries LegalEntityId now, so MDM decides whether it is the tenant's and Active.
        // Claims answers a scope mismatch with 403 FORBIDDEN, not 404, so NotReferenceable keeps THIS module's
        // convention — the place the retired headerLe != le branch sent a mismatch — rather than the other four's.
        var le = headerLe;
        switch (await legalEntities.ValidateAsync(tenant, le, authorization[0]!, context.CorrelationId, http.RequestAborted))
        {
            case LegalEntityScopeOutcome.NotReferenceable: await Error(403, "FORBIDDEN"); return;
            case LegalEntityScopeOutcome.Unavailable: await Error(503, ContractErrorCodes.DependencyUnavailable); return;
        }
        var scopeKeys = new[] { "tenantid", "legalentityid", "tenant_id", "legal_entity_id", "x-tenant-id", "x-legal-entity-id" };
        if (http.Request.Query.Keys.Any(k => k.All(c => c <= 127) && scopeKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
        { await Error(400, "INVALID_REQUEST"); return; }
        if (http.Request.RouteValues.TryGetValue("claimId", out var id) && !Uuid(id?.ToString(), out _))
        { await Error(400, "INVALID_REQUEST"); return; }
        if (HttpMethods.IsGet(http.Request.Method))
        {
            if (http.Request.Query.TryGetValue("status", out var status) && (status.Count != 1 || !Enum.GetNames<ClaimStatus>().Contains(status[0], StringComparer.Ordinal)))
            { await Error(400, "INVALID_REQUEST"); return; }
            if (http.Request.Query.TryGetValue("shipmentId", out var shipment) && (shipment.Count != 1 || !ClaimWire.Uuid(shipment[0])))
            { await Error(400, "INVALID_REQUEST"); return; }
        }
        if (HttpMethods.IsPost(http.Request.Method) && !http.Request.HasJsonContentType()) { await Error(415, "UNSUPPORTED_MEDIA_TYPE"); return; }
        context.Scope = new(tenant, le, actor);
        context.Authorization = authorization[0]!;
        context.Permissions = http.User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        loggingContext.Scope = new(tenant, le, actor);
        loggingContext.CorrelationId = context.CorrelationId;
        try { await next(http); }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) when (!http.Response.HasStarted) { await Error(500, "INTERNAL_ERROR"); }
    }
}
