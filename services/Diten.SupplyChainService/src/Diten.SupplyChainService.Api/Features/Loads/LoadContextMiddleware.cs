using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Loads;
using Diten.SupplyChainService.Domain.Features.Loads;
using Diten.SupplyChainService.Infrastructure.Features.Loads;
namespace Diten.SupplyChainService.Api.Features.Loads;
public sealed class LoadContextMiddleware(RequestDelegate next, ILogger<LoadContextMiddleware> logger)
{
    public static bool IsLoadPath(HttpContext http) => http.Request.Path.StartsWithSegments("/api/shipment-bundle/loads", StringComparison.OrdinalIgnoreCase);
    private static bool Uuid(string? text, out Guid value)
    {
        value = Guid.Empty;
        return LoadWire.Uuid(text) && Guid.TryParseExact(text, "D", out value);
    }
    private static bool Header(HttpRequest request, string name, out Guid value)
    {
        value = Guid.Empty;
        return request.Headers.TryGetValue(name, out var values) && values.Count == 1 && Uuid(values[0], out value);
    }
    private static bool Claim(HttpContext http, string name, out Guid value)
    {
        value = Guid.Empty;
        var values = http.User.FindAll(name).ToArray();
        return values.Length == 1 && Uuid(values[0].Value, out value) && value != Guid.Empty;
    }
    private static bool UniqueSignedContextFields(string authorization)
    {
        // Inspect only after JwtBearer validates the signature. Duplicate JSON members can
        // otherwise collapse to one claim during token parsing.
        try
        {
            var segment = authorization[7..].Split('.')[1].Replace('-', '+').Replace('_', '/');
            segment = segment.PadRight((segment.Length + 3) / 4 * 4, '=');
            using var payload = JsonDocument.Parse(Convert.FromBase64String(segment));
            return new[] { "tenant_id", "legal_entity_id", "sub" }.All(name =>
                payload.RootElement.EnumerateObject().Count(p => p.NameEquals(name)) == 1 && payload.RootElement.GetProperty(name).ValueKind == JsonValueKind.String && Uuid(payload.RootElement.GetProperty(name).GetString(), out var id) && id != Guid.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IndexOutOfRangeException or ArgumentException) { return false; }
    }
    public async Task InvokeAsync(HttpContext http, LoadRequestContext context, RequestContext loggingContext)
    {
        var permission = http.GetEndpoint()?.Metadata.GetMetadata<LoadPermissionAttribute>();
        if (permission is null) { await next(http); return; }
        var validCorrelation = Header(http.Request, "X-Correlation-Id", out var correlation);
        context.CorrelationId = validCorrelation ? correlation : Guid.NewGuid();
        http.Response.Headers["X-Correlation-Id"] = context.CorrelationId.ToString();
        async Task Error(int status, string code = "INVALID_REQUEST", string? message = null)
        {
            logger.LogInformation("Load request rejected {Status} {Code} {CorrelationId}", status, code, context.CorrelationId);
            http.Response.StatusCode = status;
            if (status == 401) http.Response.Headers.WWWAuthenticate = "Bearer";
            await http.Response.WriteAsJsonAsync(LoadContractError.Create(code, status, context.CorrelationId, message));
        }
        var authorization = http.Request.Headers.Authorization;
        if (authorization.Count != 1 || http.User.Identity?.IsAuthenticated != true) { await Error(401); return; }
        if (!UniqueSignedContextFields(authorization[0]!) || !Claim(http, "tenant_id", out var tenant) || !Claim(http, "legal_entity_id", out var le) || !Claim(http, "sub", out var actor) ||
            !http.User.HasClaim("permission", permission.Permission)) { await Error(403); return; }
        if (!validCorrelation) { await Error(400, message: "X-Correlation-Id must contain exactly one UUID value."); return; }
        if (!Header(http.Request, "X-Tenant-Id", out var headerTenant)) { await Error(400, message: "X-Tenant-Id must contain exactly one UUID value."); return; }
        if (!Header(http.Request, "X-Legal-Entity-Id", out var headerLe)) { await Error(400, message: "X-Legal-Entity-Id must contain exactly one UUID value."); return; }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            var keys = http.Request.Headers["Idempotency-Key"];
            if (keys.Count != 1 || keys[0] is not { } key || key.EnumerateRunes().Count() is < 1 or > 128)
            { await Error(400, message: "Idempotency-Key must contain exactly one string value of length 1 to 128."); return; }
            context.IdempotencyKey = key;
        }
        if (tenant != headerTenant || le != headerLe) { await Error(404, "LOAD_NOT_FOUND"); return; }
        if (http.Request.RouteValues.TryGetValue("loadId", out var id) && !Uuid(id?.ToString(), out _)) { await Error(400); return; }
        if (http.Request.Query.Keys.Any(k => new[] { "tenantId", "tenant_id", "X-Tenant-Id", "legalEntityId", "legal_entity_id", "X-Legal-Entity-Id" }.Contains(k, StringComparer.OrdinalIgnoreCase)))
        { await Error(400); return; }
        if (HttpMethods.IsGet(http.Request.Method) && http.Request.Query.TryGetValue("status", out var statuses) &&
            (statuses.Count != 1 || !Enum.GetNames<LoadStatus>().Contains(statuses[0], StringComparer.Ordinal))) { await Error(400); return; }
        if (HttpMethods.IsPost(http.Request.Method) && !http.Request.HasJsonContentType()) { await Error(415); return; }
        if (HttpMethods.IsGet(http.Request.Method) && http.Request.Query.TryGetValue("carrierId", out var carriers) && (carriers.Count != 1 || !LoadWire.Uuid(carriers[0]))) { await Error(400); return; }
        context.Authorization = authorization[0]!;
        context.Scope = new(tenant, le, actor);
        // The four shared pipeline behaviors read this existing scoped diagnostic context.
        // Populate it only after Load authorization; no Shipment request state is reused.
        loggingContext.Scope = new(tenant, le, actor);
        loggingContext.CorrelationId = context.CorrelationId;
        try
        {
            await next(http);
            logger.LogInformation("Load request completed {Status} {CorrelationId}", http.Response.StatusCode, context.CorrelationId);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) when (!http.Response.HasStarted) { await Error(500, "INTERNAL_ERROR"); }
    }
}
