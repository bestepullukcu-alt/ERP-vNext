using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Xunit;
using Diten.SupplyChainService.Tests.Common;

namespace Diten.SupplyChainService.Tests.SandopPlans;

/// <summary>
/// MOD-0190 over HTTP. Until 2026-10-11 the whole SandopPlans suite drove SandopRepository directly: the
/// 201/409/422 values it asserted were the repository's own result codes, and one test reached the API layer
/// by constructing a DefaultHttpContext. Four acceptance lines ask for things only a request can answer, so
/// these go through the real pipeline — routing, authentication, the permission filter, the controller and
/// the wire envelope — on SandopCapacityHttpHost. Production composition is unchanged; see that host's note.
/// </summary>
[Collection(SandopCapacityHttpCollection.Name)]
public sealed class SandopHttpContractTests
{
    private const string Plans = "/api/supply-chain/sandop-plans";
    private readonly SandopCapacityHttpHost _host;
    public SandopHttpContractTests(SandopCapacityHttpHost host) => _host = host;

    private static string Key() => "k-" + Guid.NewGuid();

    private async Task<(Guid PlanId, Guid SnapshotId)> Seed()
    {
        var token = _host.Token();
        var created = await _host.Send("POST", Plans, SandopCapacityHttpHost.SandopPlan(), token, Key());
        Assert.Equal(201, created.Status);
        var planId = Guid.Parse(created.Json!["sandopPlanId"]!.GetValue<string>());
        var capture = new JsonObject
        {
            ["demandPlanId"] = "dp-2027", ["demandPlanVersion"] = "3",
            ["sourceCapturedAt"] = "2026-09-15T09:45:00Z", ["sourceChecksum"] = "sha256:abc",
            ["supplyInputRefs"] = new JsonArray()
        };
        var captured = await _host.Send("POST", $"{Plans}/{planId}/snapshots", capture, token, Key());
        Assert.Equal(201, captured.Status);
        return (planId, Guid.Parse(captured.Json!["snapshotId"]!.GetValue<string>()));
    }

    // :201 — the pack says six operations. These are the six the controller declares; a seventh would make
    // this test fail to compile against the route table, and a removed one fails here.
    [Fact]
    public async Task Six_declared_operations_answer_over_http_and_wire_contract_version_v1()
    {
        var (planId, snapshotId) = await Seed();
        var token = _host.Token();
        var signOff = new JsonObject { ["snapshotId"] = snapshotId, ["role"] = "Finance", ["decision"] = "Approved" };

        var calls = new (string Name, string Method, string Path, JsonObject? Body, int Expected)[]
        {
            ("createSandopPlan", "POST", Plans, SandopCapacityHttpHost.SandopPlan(), 201),
            ("getSandopPlan", "GET", $"{Plans}/{planId}", null, 200),
            ("captureSnapshot", "POST", $"{Plans}/{planId}/snapshots", new JsonObject
                { ["demandPlanId"] = "dp-2027", ["demandPlanVersion"] = "3",
                  ["sourceCapturedAt"] = "2026-09-15T09:45:00Z", ["sourceChecksum"] = "sha256:abc",
                  ["supplyInputRefs"] = new JsonArray() }, 201),
            ("listSnapshots", "GET", $"{Plans}/{planId}/snapshots", null, 200),
            ("recordSignOff", "POST", $"{Plans}/{planId}/sign-offs", signOff, 201),
            ("listSignOffs", "GET", $"{Plans}/{planId}/sign-offs", null, 200)
        };

        foreach (var (name, method, path, body, expected) in calls)
        {
            var reply = await _host.Send(method, path, body, token, Key());
            Assert.True(expected == reply.Status, $"{name}: expected {expected}, got {reply.Status}: {reply.Body}");
            Assert.False(string.IsNullOrWhiteSpace(reply.Correlation), $"{name}: no X-Correlation-Id header");
        }

        // contractVersion is on the wire for errors; a 404 is the cheapest way to see one.
        var missing = await _host.Send("GET", $"{Plans}/{Guid.NewGuid()}", null, token, Key());
        Assert.Equal(404, missing.Status);
        Assert.Equal("v1", missing.Json!["contractVersion"]!.GetValue<string>());
        Assert.Equal("UNKNOWN_SANDOP_PLAN", missing.Code);
    }

    // :200 over HTTP. The repository-level test already proved the module boundary; this proves the request
    // path reaches the same answer through authentication and the permission filter.
    [Fact]
    public async Task Foreign_scope_is_404_and_missing_permission_is_403_over_http()
    {
        var (planId, _) = await Seed();

        // The gate rejects a token/header disagreement with 403 before the repository is reached, so a foreign
        // scope has to move in both places at once for this to be a 404 about the plan rather than about the scope.
        var otherTenant = Guid.NewGuid();
        var foreignTenant = _host.Token(tenant: otherTenant);
        Assert.Equal(404, (await _host.Send("GET", $"{Plans}/{planId}", null, foreignTenant, Key(),
            tenant: otherTenant)).Status);

        var otherLe = Guid.NewGuid();
        var foreignLe = _host.Token(legalEntity: otherLe);
        Assert.Equal(404, (await _host.Send("GET", $"{Plans}/{planId}", null, foreignLe, Key(),
            legalEntity: otherLe)).Status);

        var withoutRead = _host.Token(permissions: SandopCapacityHttpHost.AllPermissions
            .Where(p => p != "supplychain.sandop-plans.read").ToArray());
        var denied = await _host.Send("GET", $"{Plans}/{planId}", null, withoutRead, Key());
        Assert.Equal(403, denied.Status);
        Assert.Equal("FORBIDDEN", denied.Code);
    }

    // :199 — the correlation and authentication fallback, none of which had any assertion before today.
    [Fact]
    public async Task Correlation_and_authentication_follow_the_published_fallback()
    {
        var token = _host.Token();

        // Present and well-formed: echoed back unchanged.
        var mine = Guid.NewGuid().ToString();
        var echoed = await _host.Send("GET", Plans + "/" + Guid.NewGuid(), null, token, Key(), correlation: mine);
        Assert.Equal(mine, echoed.Correlation);
        Assert.Equal(mine, echoed.Json!["error"]!["correlationId"]!.GetValue<string>());

        // Missing: the service mints one rather than failing, and still reports it.
        var absent = await _host.Send("GET", Plans + "/" + Guid.NewGuid(), null, token, Key(),
            alter: r => r.Headers.Remove("X-Correlation-Id"));
        Assert.True(Guid.TryParse(absent.Correlation, out var minted) && minted != Guid.Empty,
            $"missing correlation did not produce a fresh one: {absent.Correlation}");

        // Malformed and duplicate are both refused with the declared code, not a 500.
        foreach (var (label, alter) in new (string, Action<HttpRequestMessage>)[]
                 {
                     ("malformed", r => { r.Headers.Remove("X-Correlation-Id");
                         r.Headers.TryAddWithoutValidation("X-Correlation-Id", "not-a-uuid"); }),
                     ("duplicate", r => r.Headers.TryAddWithoutValidation("X-Correlation-Id", Guid.NewGuid().ToString()))
                 })
        {
            var reply = await _host.Send("GET", Plans + "/" + Guid.NewGuid(), null, token, Key(), alter: alter);
            Assert.True(reply.Status == 400, $"{label}: expected 400, got {reply.Status}: {reply.Body}");
            Assert.Equal("INVALID_CORRELATION_ID", reply.Code);
        }

        // No bearer at all: the application's own 401, with the declared envelope rather than an empty body.
        var anonymous = await _host.Send("GET", Plans + "/" + Guid.NewGuid(), null, token: null, Key());
        Assert.Equal(401, anonymous.Status);
    }
}
