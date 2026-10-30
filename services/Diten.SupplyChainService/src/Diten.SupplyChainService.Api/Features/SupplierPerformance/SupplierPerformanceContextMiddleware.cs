using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Api.Features.SupplierPerformance;

/// <summary>
/// MOD-0147 §0.5 stages 1–3b and the header / path / query part of stage 4, first failing stage wins, on the house R-2 pattern
/// (LoadContextMiddleware): 1 token → 401; 2 signed tenant_id, sub and the operation's permission → 403; 3 header syntax
/// (X-Correlation-Id, X-Tenant-Id, X-Legal-Entity-Id, Idempotency-Key on commands, If-Match on submit) → 400; 3a X-Tenant-Id ≠ signed tenant → 404,
/// no lookup; 3b MDM: company not referenceable → 404, MDM unreachable or MDM 403 → 503 DEPENDENCY_UNAVAILABLE. The 404s
/// do not disclose: the operation's declared 404 object, NOT_FOUND for lists. The response header and the error body carry
/// the same correlation id: the valid one sent, or one generated UUID.
/// </summary>
public sealed class SupplierPerformanceContextMiddleware(RequestDelegate next, ILogger<SupplierPerformanceContextMiddleware> logger)
{
    public const int MaxIdempotencyKeyLength = 200;
    public static bool IsSupplierPerformancePath(HttpContext http) => http.Request.Path.StartsWithSegments("/api/supplier-performance", StringComparison.OrdinalIgnoreCase);
    private static readonly string[] ScopeQueryNames = ["tenantId", "tenant_id", "X-Tenant-Id", "legalEntityId", "legal_entity_id", "X-Legal-Entity-Id"];

    private static bool Uuid(string? text, out Guid value)
    {
        value = Guid.Empty;
        return SupplierPerformanceWire.Uuid(text) && Guid.TryParseExact(text, "D", out value);
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
        // Inspect only after JwtBearer validated the signature. Duplicate JSON members could otherwise collapse to one claim.
        try
        {
            var segment = authorization[7..].Split('.')[1].Replace('-', '+').Replace('_', '/');
            segment = segment.PadRight((segment.Length + 3) / 4 * 4, '=');
            using var payload = JsonDocument.Parse(Convert.FromBase64String(segment));
            return new[] { "tenant_id", "sub" }.All(name =>
                payload.RootElement.EnumerateObject().Count(p => p.NameEquals(name)) == 1 && payload.RootElement.GetProperty(name).ValueKind == JsonValueKind.String
                && Uuid(payload.RootElement.GetProperty(name).GetString(), out var id) && id != Guid.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IndexOutOfRangeException or ArgumentException or InvalidOperationException) { return false; }
    }
    /// <summary>§0.5 key rule: one value, 1..200 Unicode code points, as sent; a key of only White_Space code points is refused.</summary>
    public static bool ValidIdempotencyKey(string? key) =>
        key is not null && key.EnumerateRunes().Count() is >= 1 and <= MaxIdempotencyKeyLength && !key.EnumerateRunes().All(Rune.IsWhiteSpace);

    public async Task InvokeAsync(HttpContext http, SupplierPerformanceRequestContext context, RequestContext loggingContext, ILegalEntityScopeValidator legalEntities)
    {
        var permission = http.GetEndpoint()?.Metadata.GetMetadata<SupplierPerformancePermissionAttribute>();
        if (permission is null) { await next(http); return; }
        var validCorrelation = Header(http.Request, "X-Correlation-Id", out var correlation);
        context.CorrelationId = validCorrelation ? correlation : Guid.NewGuid();
        http.Response.Headers["X-Correlation-Id"] = context.CorrelationId.ToString();
        async Task Error(int status, string code)
        {
            logger.LogInformation("Supplier performance request rejected {Status} {Code} {CorrelationId}", status, code, context.CorrelationId);
            http.Response.StatusCode = status;
            if (status == 401) http.Response.Headers.WWWAuthenticate = "Bearer";
            await http.Response.WriteAsJsonAsync(SupplierPerformanceContractError.Create(code, status, context.CorrelationId));
        }
        // 1
        var authorization = http.Request.Headers.Authorization;
        if (authorization.Count != 1 || http.User.Identity?.IsAuthenticated != true) { await Error(401, SupplierPerformanceErrorCodes.Unauthenticated); return; }
        // 2
        if (!UniqueSignedContextFields(authorization[0]!) || !Claim(http, "tenant_id", out var tenant) || !Claim(http, "sub", out var actor)
            || !http.User.HasClaim("permission", permission.Permission)) { await Error(403, SupplierPerformanceErrorCodes.Forbidden); return; }
        // 3
        if (!validCorrelation || !Header(http.Request, "X-Tenant-Id", out var headerTenant) || !Header(http.Request, "X-Legal-Entity-Id", out var legalEntity))
        { await Error(400, SupplierPerformanceErrorCodes.InvalidRequest); return; }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            var keys = http.Request.Headers["Idempotency-Key"];
            if (keys.Count != 1 || !ValidIdempotencyKey(keys[0])) { await Error(400, SupplierPerformanceErrorCodes.InvalidRequest); return; }
            context.IdempotencyKey = keys[0]!;
            if (http.Request.RouteValues.ContainsKey("evaluationId"))
            {
                var ifMatch = http.Request.Headers.IfMatch;
                if (ifMatch.Count != 1 || !SupplierPerformanceWire.IfMatch(ifMatch[0], out var expectedVersion)) { await Error(400, SupplierPerformanceErrorCodes.InvalidRequest); return; }
                context.ExpectedVersion = expectedVersion;
            }
        }
        // 3a
        if (tenant != headerTenant) { await Error(404, permission.ScopeNotFoundCode); return; }
        // 3b — R-2: the company comes only from X-Legal-Entity-Id; MDM says whether it is the tenant's and Active.
        switch (await legalEntities.ValidateAsync(tenant, legalEntity, authorization[0]!, context.CorrelationId, http.RequestAborted))
        {
            case LegalEntityScopeOutcome.NotReferenceable: await Error(404, permission.ScopeNotFoundCode); return;
            case LegalEntityScopeOutcome.Unavailable: await Error(503, SupplierPerformanceErrorCodes.DependencyUnavailable); return;
        }
        // 4 (path, query, media type; the body schema is the validator's)
        foreach (var name in new[] { "evaluationId", "scorecardId" })
            if (http.Request.RouteValues.TryGetValue(name, out var id) && !Uuid(id?.ToString(), out _)) { await Error(400, SupplierPerformanceErrorCodes.InvalidRequest); return; }
        if (http.Request.Query.Keys.Any(k => ScopeQueryNames.Contains(k, StringComparer.OrdinalIgnoreCase)) || http.Request.Query.Any(q => q.Value.Count != 1))
        { await Error(400, SupplierPerformanceErrorCodes.InvalidRequest); return; }
        if (HttpMethods.IsPost(http.Request.Method) && !http.Request.HasJsonContentType()) { await Error(400, SupplierPerformanceErrorCodes.InvalidRequest); return; }
        context.Authorization = authorization[0]!;
        context.Scope = new(tenant, legalEntity, actor);
        // The shared pipeline behaviors and the central-audit scope read this diagnostic context; filled only after authorization.
        loggingContext.Scope = new(tenant, legalEntity, actor);
        loggingContext.CorrelationId = context.CorrelationId;
        try
        {
            await next(http);
            logger.LogInformation("Supplier performance request completed {Status} {CorrelationId}", http.Response.StatusCode, context.CorrelationId);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) when (!http.Response.HasStarted) { await Error(500, SupplierPerformanceErrorCodes.InternalError); }
    }
}
