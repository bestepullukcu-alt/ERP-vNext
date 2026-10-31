using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;

/// <summary>
/// MOD-0147 §0.0 D2 — reads <c>GET /api/suppliers/{supplierId}/eligibility</c> (SUPPLIER 1.3.0, MOD-0140) through the Gateway
/// (<see cref="BaseUrlKey"/>, the reference-base-URL pattern; no new port) with the caller's token and the validated legal
/// entity. Read only: nothing of Procurement's answer is stored but the canonical supplier code.
/// <list type="bullet">
/// <item>200 matching <c>SupplierEligibility</c>, naming the requested supplier, relationship present → its canonical code.
/// Any relationship status and onboarding status may be evaluated (Q1, Q2); eligibleForTransactions is not used.</item>
/// <item>200 with relationshipExists ≠ true, or 404 whose body code is UNKNOWN_SUPPLIER → 404 UNKNOWN_SUPPLIER.</item>
/// <item>403 (no procurement.suppliers.eligibility.read, or LEGAL_ENTITY_LOOKUP_FORBIDDEN) → 403 FORBIDDEN, never hidden as 503.</item>
/// <item>Everything else — another 404 (UNKNOWN_LEGAL_ENTITY, a route 404 before MOD-0140 DL-3, no body), a malformed or
/// mismatched 200, 400/401/5xx, timeout, network, no configured base URL → 503 SUPPLIER_BASE_UNAVAILABLE (fail closed).</item>
/// </list>
/// </summary>
public sealed class SupplierEligibilityReader(HttpClient client, IConfiguration configuration, SupplierPerformanceRequestContext context) : ISupplierEligibilityReader
{
    public const string BaseUrlKey = "SupplierPerformance:SupplierBaseUrl";
    private static readonly string[] RelationshipStatuses = ["Active", "OnHold", "Blocked", "Inactive"];
    private static readonly string[] OnboardingStatuses = ["Draft", "InReview", "Approved", "Rejected"];
    private static SupplierPerformanceFailure Unavailable() => new(503, SupplierPerformanceErrorCodes.SupplierBaseUnavailable);
    private static SupplierPerformanceFailure Unknown() => new(404, SupplierPerformanceErrorCodes.UnknownSupplier);

    public async Task<string> CanonicalSupplierIdAsync(string requestedSupplierId, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration[BaseUrlKey], UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https")) throw Unavailable();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, "api/suppliers/" + Uri.EscapeDataString(requestedSupplierId) + "/eligibility"));
        request.Headers.TryAddWithoutValidation("Authorization", context.Authorization);
        request.Headers.Add("X-Tenant-Id", context.Scope.TenantId.ToString("D"));
        request.Headers.Add("X-Legal-Entity-Id", context.Scope.LegalEntityId.ToString("D"));
        request.Headers.Add("X-Correlation-Id", context.CorrelationId.ToString("D"));
        string body;
        HttpStatusCode status;
        try
        {
            using var response = await client.SendAsync(request, ct);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        return status switch
        {
            HttpStatusCode.OK => Canonical(body, requestedSupplierId),
            HttpStatusCode.NotFound when ErrorCode(body) == "UNKNOWN_SUPPLIER" => throw Unknown(),
            HttpStatusCode.Forbidden => throw new SupplierPerformanceFailure(403, SupplierPerformanceErrorCodes.Forbidden),
            _ => throw Unavailable()
        };
    }

    private static string? ErrorCode(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string Canonical(string body, string requested)
    {
        JsonElement e;
        try { using var json = JsonDocument.Parse(body); e = json.RootElement.Clone(); }
        catch (JsonException) { throw Unavailable(); }
        if (e.ValueKind != JsonValueKind.Object) throw Unavailable();
        bool Has(string name, out JsonElement value) => e.TryGetProperty(name, out value);
        bool Text(string name, out string text) { text = ""; if (!Has(name, out var v) || v.ValueKind != JsonValueKind.String) return false; text = v.GetString()!; return true; }
        bool OneOf(string name, string[] values, bool nullable) =>
            Has(name, out var v) && ((nullable && v.ValueKind == JsonValueKind.Null) || (v.ValueKind == JsonValueKind.String && values.Contains(v.GetString(), StringComparer.Ordinal)));
        bool Boolean(string name, bool nullable) => Has(name, out var v) && (v.ValueKind is JsonValueKind.True or JsonValueKind.False || (nullable && v.ValueKind == JsonValueKind.Null));

        if (!Boolean("relationshipExists", false)) throw Unavailable();
        // MOD-0140 §3.1: on this endpoint relationshipExists is always true; a false is the "no relationship" answer (defensive).
        if (e.GetProperty("relationshipExists").ValueKind != JsonValueKind.True) throw Unknown();
        if (!Text("supplierId", out var supplierId) || !SupplierPerformanceWire.IsCanonicalCode(supplierId)
            || !Text("supplierMasterId", out var masterId) || !SupplierPerformanceWire.Uuid(masterId)
            || !OneOf("status", RelationshipStatuses, false) || !OneOf("onboardingStatus", OnboardingStatuses, true)
            || !Boolean("purchasingEnabled", true) || !Boolean("eligibleForTransactions", false)
            || (Has("contractVersion", out var version) && version.ValueKind != JsonValueKind.String))
            throw Unavailable();
        // The answer must name the requested supplier (Natig P1, review F-2): the code for a SUP- request, the master uuid for a uuid.
        var same = SupplierPerformanceWire.Uuid(requested)
            ? Guid.Parse(masterId) == Guid.Parse(requested)
            : string.Equals(supplierId, requested, StringComparison.Ordinal);
        return same ? supplierId : throw Unavailable();
    }
}
