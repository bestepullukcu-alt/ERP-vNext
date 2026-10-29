using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>
/// A stand-in for Procurement's <c>GET /api/suppliers/{supplierId}/eligibility</c>, written from the SUPPLIER 1.3.0 draft that
/// MOD-0140 DL-1 froze on its own branch (<c>origin/feature/mvp2/supplier-dl1-contract-130</c> @ cfd917df3,
/// <c>docs/analysis/contracts/supplier.openapi.yaml</c>, sha256 7204e6d4d795218ce1560d6e6a41ee51b709c69830831606c59fc7730bfd7035;
/// not on main yet, not copied here). Declared answers: 200 SupplierEligibility, 403 LegalEntityLookupForbidden,
/// 404 UnknownSupplierOrLegalEntity, 503 DependencyUnavailable. Every other answer in <see cref="Answer"/> is a hand-written
/// stub for an undeclared case of T-0147-V1-ELIG-01 (MOD-0147 §0.0, review F-3). Records each call.
/// </summary>
internal sealed class FakeProcurement : HttpMessageHandler
{
    public sealed record Call(string Path, string? Authorization, string? Tenant, string? LegalEntity, string? Correlation);

    public const string Code = "SUP-2001";
    public const string MasterId = "0b6f3c2e-6d1a-4c8e-9a51-2f4e7d9b1a10";
    public ConcurrentQueue<Call> Calls { get; } = new();
    /// <summary>Status and body (null = no body) for the next requests; default: the declared 200 for <see cref="Code"/>.</summary>
    public Func<string, (HttpStatusCode Status, string? Body)> Answer { get; set; } = _ => (HttpStatusCode.OK, Eligibility().ToJsonString());
    public Exception? Throw { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    // The 1.3.0 example of getSupplierEligibility (status Blocked, eligibleForTransactions false) — evaluable under Q1/Q2.
    public static JsonObject Eligibility(string supplierId = Code, string? masterId = MasterId, string? status = "Blocked", string? onboarding = "Approved",
        bool relationship = true) => new()
    {
        ["supplierId"] = supplierId, ["supplierMasterId"] = masterId, ["relationshipExists"] = relationship, ["status"] = status,
        ["onboardingStatus"] = onboarding, ["purchasingEnabled"] = true, ["eligibleForTransactions"] = false, ["contractVersion"] = "v1"
    };

    public static string Error(string code) => new JsonObject
    { ["error"] = new JsonObject { ["code"] = code, ["message"] = "stub", ["correlationId"] = "req-1" }, ["contractVersion"] = "v1" }.ToJsonString();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string? One(string name) => request.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : null;
        var path = request.RequestUri!.AbsolutePath;
        Calls.Enqueue(new(path, One("Authorization"), One("X-Tenant-Id"), One("X-Legal-Entity-Id"), One("X-Correlation-Id")));
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
        if (Throw is not null) throw Throw;
        const string prefix = "/api/suppliers/", suffix = "/eligibility";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || !path.EndsWith(suffix, StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        var (status, body) = Answer(Uri.UnescapeDataString(path[prefix.Length..^suffix.Length]));
        var response = new HttpResponseMessage(status);
        if (body is not null) response.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return response;
    }
}
