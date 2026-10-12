using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>The primary handler of the e-signature HttpClient (the real <c>GatewayESignClient</c> runs on top of it).</summary>
public sealed class FakeESignHandler(FakeESignGateway gateway) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(gateway.Handle(request));
}

/// <summary>
/// An in-memory MOD-0022 behind the Gateway, with the two reads 0175 makes (ESIGN-BUNDLE §4.5 getSignature, §4.15
/// getEffectivePolicy): bearer required (no token → 401), the caller's tenant from the token, the read key
/// <c>esign.signatures.read</c> for a signature (403 without), 404 for another tenant's id. Policies per tenant + action
/// (default: not required — the Q1 / Q2 suites submit without a signature); signatures are recorded by the tests the way
/// Platform records them (signer, subject, meaning, the subject hash computed from 0175's own provider answer).
/// </summary>
public sealed class FakeESignGateway
{
    public sealed record Record(Guid Tenant, string Id, Guid Signer, string Module, string SubjectType, string SubjectId, string Meaning, string SubjectHash)
    {
        public bool Invalidated { get; set; }
    }

    private readonly ConcurrentDictionary<(Guid Tenant, string Action), (bool Required, string? Meaning)> _policies = new();
    private readonly ConcurrentDictionary<string, Record> _records = new();

    public ConcurrentQueue<(string Method, string Path, string? Authorization)> Requests { get; } = new();

    public bool Down { get; set; }

    public void Policy(Guid tenant, string actionCode, bool required, string? meaning) => _policies[(tenant, actionCode)] = (required, meaning);

    public string Sign(Guid tenant, Guid signer, string subjectId, string meaning, string subjectHash,
        string module = "quarantine", string subjectType = "quarantine.decision")
    {
        var id = Guid.NewGuid().ToString("D");
        _records[id] = new Record(tenant, id, signer, module, subjectType, subjectId, meaning, subjectHash);
        return id;
    }

    public void Invalidate(string id) => _records[id].Invalidated = true;

    public HttpResponseMessage Handle(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        Requests.Enqueue((request.Method.Method, request.RequestUri.PathAndQuery, request.Headers.Authorization?.ToString()));
        if (Down) return Reply(503, null, "SERVICE_UNAVAILABLE");
        if (TestJwt.Claims(request.Headers.Authorization?.ToString()) is not { } caller) return Reply(401, null, "UNAUTHORIZED");

        if (request.Method == HttpMethod.Get && path == "/api/v1/esign/policy/effective")
        {
            if (!caller.Permissions.Contains("esign.signatures.sign")) return Reply(403, null, "FORBIDDEN");
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            var (required, meaning) = _policies.TryGetValue((caller.Tenant, query["actionCode"] ?? ""), out var p) ? p : (false, null);
            return Reply(200, new JsonObject { ["required"] = required, ["meaningCode"] = meaning, ["visualMode"] = "optional" });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/esign/signatures/", StringComparison.Ordinal))
        {
            if (!caller.Permissions.Contains("esign.signatures.read")) return Reply(403, null, "FORBIDDEN");
            var id = path["/api/v1/esign/signatures/".Length..];
            if (!_records.TryGetValue(id, out var r) || r.Tenant != caller.Tenant) return Reply(404, null, "ESIGN_NOT_FOUND");
            return Reply(200, new JsonObject
            {
                ["id"] = r.Id,
                ["signatureNumber"] = "ESIG-2026-000001",
                ["subject"] = new JsonObject { ["module"] = r.Module, ["subjectType"] = r.SubjectType, ["subjectId"] = r.SubjectId },
                ["meaningCode"] = r.Meaning,
                ["signer"] = new JsonObject { ["userId"] = r.Signer.ToString("D"), ["login"] = "signer", ["name"] = "Signer" },
                ["subjectHashAlgorithm"] = "CanonicalJsonSha256",
                ["subjectHash"] = r.SubjectHash,
                ["signatureDigest"] = "d",
                ["invalidation"] = r.Invalidated ? new JsonObject { ["reason"] = "signed by mistake" } : null
            });
        }

        return Reply(404, null, "ROUTE_NOT_FOUND");
    }

    private static HttpResponseMessage Reply(int status, JsonNode? data, string? reason = null)
        => new((HttpStatusCode)status)
        {
            Content = new StringContent(new JsonObject { ["data"] = data, ["errors"] = new JsonArray(), ["reason_code"] = reason }.ToJsonString(), Encoding.UTF8, "application/json")
        };
}

/// <summary>Reads the claims of a test bearer token (the services validate the signature; the fakes only read).</summary>
public static class TestJwt
{
    public sealed record Caller(Guid User, Guid Tenant, IReadOnlySet<string> Permissions);

    public static Caller? Claims(string? authorization)
    {
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var parts = authorization["Bearer ".Length..].Split('.');
        if (parts.Length != 3) return null;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var claims = JsonNode.Parse(Convert.FromBase64String(payload))!.AsObject();
        var permissions = claims["permission"] switch
        {
            JsonArray array => array.Select(p => p!.GetValue<string>()).ToHashSet(StringComparer.Ordinal),
            JsonValue value => new HashSet<string>(StringComparer.Ordinal) { value.GetValue<string>() },
            _ => new HashSet<string>(StringComparer.Ordinal)
        };
        return Guid.TryParse(claims["sub"]?.GetValue<string>(), out var user) && Guid.TryParse(claims["tenant_id"]?.GetValue<string>(), out var tenant)
            ? new Caller(user, tenant, permissions)
            : null;
    }
}
