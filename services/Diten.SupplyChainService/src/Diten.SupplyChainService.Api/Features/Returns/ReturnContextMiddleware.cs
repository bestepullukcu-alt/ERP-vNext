using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
namespace Diten.SupplyChainService.Api.Features.Returns;
public sealed class ReturnContextMiddleware(RequestDelegate next, ILogger<ReturnContextMiddleware> logger)
{
    public static bool IsReturnPath(HttpContext http) => http.Request.Path.StartsWithSegments("/api/shipment-bundle/returns", StringComparison.OrdinalIgnoreCase);
    private static bool ValidKey(string key)
    {
        var span = key.AsSpan(); var count = 0;
        while (!span.IsEmpty) { if (Rune.DecodeFromUtf16(span, out _, out var consumed) != System.Buffers.OperationStatus.Done) return false; count++; span = span[consumed..]; }
        return count is >= 1 and <= 128;
    }
    private static bool Uuid(string? text, out Guid value)
    {
        value = Guid.Empty;
        return ReturnWire.Uuid(text) && Guid.TryParseExact(text, "D", out value);
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
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
            var segment = authorization[7..].Split('.')[1].Replace('-', '+').Replace('_', '/');
            segment = segment.PadRight((segment.Length + 3) / 4 * 4, '=');
            using var payload = JsonDocument.Parse(Convert.FromBase64String(segment));
            return new[] { "tenant_id", "sub" }.All(name =>
                payload.RootElement.EnumerateObject().Count(p => p.NameEquals(name)) == 1 && payload.RootElement.GetProperty(name).ValueKind == JsonValueKind.String && Uuid(payload.RootElement.GetProperty(name).GetString(), out var id) && id != Guid.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IndexOutOfRangeException or ArgumentException or InvalidOperationException) { return false; }
    }
    public async Task InvokeAsync(HttpContext http, ReturnRequestContext context, RequestContext loggingContext, ILegalEntityScopeValidator legalEntities)
    {
        var permission = http.GetEndpoint()?.Metadata.GetMetadata<ReturnPermissionAttribute>();
        if (permission is null) { await next(http); return; }
        var validCorrelation = Header(http.Request, "X-Correlation-Id", out var correlation);
        context.CorrelationId = validCorrelation ? correlation : Guid.NewGuid();
        http.Response.Headers["X-Correlation-Id"] = context.CorrelationId.ToString();
        async Task Error(int status, string code = "INVALID_REQUEST", string? message = null)
        {
            logger.LogInformation("Return request rejected {Status} {Code} {CorrelationId}", status, code, context.CorrelationId);
            http.Response.StatusCode = status;
            if (status == 401) http.Response.Headers.WWWAuthenticate = "Bearer";
            await http.Response.WriteAsJsonAsync(ReturnContractError.Create(code, status, context.CorrelationId, message));
        }
        var authorization = http.Request.Headers.Authorization;
        if (authorization.Count != 1 || http.User.Identity?.IsAuthenticated != true) { await Error(401); return; }
        if (!UniqueSignedContextFields(authorization[0]!) || !Claim(http, "tenant_id", out var tenant) || !Claim(http, "sub", out var actor) ||
            !http.User.HasClaim("permission", permission.Permission)) { await Error(403); return; }
        if (!validCorrelation) { await Error(400, message: "X-Correlation-Id must contain exactly one UUID value."); return; }
        if (!Header(http.Request, "X-Tenant-Id", out var headerTenant)) { await Error(400, message: "X-Tenant-Id must contain exactly one UUID value."); return; }
        if (!Header(http.Request, "X-Legal-Entity-Id", out var headerLe)) { await Error(400, message: "X-Legal-Entity-Id must contain exactly one UUID value."); return; }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            var keys = http.Request.Headers["Idempotency-Key"];
            if (keys.Count != 1 || keys[0] is not { } key || !ValidKey(key))
            { await Error(400, message: "Idempotency-Key must contain exactly one string value of length 1 to 128."); return; }
            context.IdempotencyKey = key;
        }
        if (tenant != headerTenant) { await Error(404, "RETURN_NOT_FOUND"); return; }
        // R-2: LegalEntityId artik token'da degil, istekle gelir. Kaynak tek: X-Legal-Entity-Id. Tenant'a ait
        // ve Active oldugunu MDM soyler; Valid disindaki her sonuc reddeder (fail-closed).
        var le = headerLe;
        switch (await legalEntities.ValidateAsync(tenant, le, authorization[0]!, context.CorrelationId, http.RequestAborted))
        {
            // MDM yabanci ile pasifi ayirt etmez (TenantFilter lookup'in icinde), ikisi de modulun kendi
            // NOT_FOUND'u olarak doner - eski le != headerLe dalinin gittigi yer, ifsa etmeyen cevap.
            case LegalEntityScopeOutcome.NotReferenceable: await Error(404, "RETURN_NOT_FOUND"); return;
            case LegalEntityScopeOutcome.Unavailable: await Error(503, ContractErrorCodes.DependencyUnavailable); return;
        }
        if (http.Request.RouteValues.TryGetValue("returnId", out var id) && !Uuid(id?.ToString(), out _)) { await Error(400); return; }
        if (http.Request.Query.Keys.Any(k => k.All(c => c <= 127) && new[] { "tenantId", "tenant_id", "X-Tenant-Id", "legalEntityId", "legal_entity_id", "X-Legal-Entity-Id" }.Contains(k, StringComparer.OrdinalIgnoreCase)))
        { await Error(400); return; }
        if (HttpMethods.IsGet(http.Request.Method) && http.Request.Query.TryGetValue("status", out var statuses) &&
            (statuses.Count != 1 || !Enum.GetNames<ReturnStatus>().Contains(statuses[0], StringComparer.Ordinal))) { await Error(400); return; }
        if (HttpMethods.IsPost(http.Request.Method) && !http.Request.HasJsonContentType()) { await Error(415); return; }
        if (HttpMethods.IsGet(http.Request.Method) && http.Request.Query.TryGetValue("shipmentId", out var shipments) && (shipments.Count != 1 || !ReturnWire.Uuid(shipments[0]))) { await Error(400); return; }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            http.Request.EnableBuffering();
            try
            {
                using var body = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
                bool transition = http.Request.RouteValues.ContainsKey("returnId");
                if (!(transition ? ReturnWire.TransitionValid(body.RootElement) : ReturnWire.CreateValid(body.RootElement))) { await Error(400); return; }
                if (transition)
                {
                    var grant = ReturnPermissions.ForTarget(body.RootElement.GetProperty("targetStatus").GetString()!);
                    if (grant is not null && !http.User.HasClaim("permission", grant)) { await Error(403); return; }
                }
            }
            catch (JsonException) { await Error(400); return; }
            finally { http.Request.Body.Position = 0; }
        }
        context.Authorization = authorization[0]!;
        context.Scope = new(tenant, le, actor);
        // The four shared pipeline behaviors read this existing scoped diagnostic context.
        // Populate it only after Return authorization; no Shipment request state is reused.
        loggingContext.Scope = new(tenant, le, actor);
        loggingContext.CorrelationId = context.CorrelationId;
        try
        {
            await next(http);
            logger.LogInformation("Return request completed {Status} {CorrelationId}", http.Response.StatusCode, context.CorrelationId);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) when (!http.Response.HasStarted) { await Error(500, "INTERNAL_ERROR"); }
    }
}
