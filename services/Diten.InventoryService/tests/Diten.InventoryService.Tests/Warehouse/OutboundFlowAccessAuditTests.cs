using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-5 AC-73 — the general rules on the DL-5 outbound / package / pick paths: another tenant's or company's record is 404, a company selector
/// mismatch 403 CROSS_LE_FORBIDDEN, no token 401, a missing key 403 per action; every keyed write replays (same key + body → 200, no new row, no second
/// central record), another body → 400.
/// </summary>
public sealed class OutboundFlowAccessAuditTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundFlowAccessAuditTests(WarehouseApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC73_the_DL5_paths_are_tenant_and_company_bound_and_keyed_per_action()
    {
        var (s, f, _, _) = await PickTaskTests.FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        await f.PickAllAsync(id);
        var package = await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 30))), 201);
        var packageId = package.GetProperty("packageId").GetString()!;

        // another tenant (and another company of a fresh tenant) never sees them — 404
        var other = await new OutboundScenario(_factory).ReadyAsync();
        var otherFlow = new OutboundFlow(other);
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(other.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}/packages", otherFlow.Supervisor), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(otherFlow.CancelAsync(id, ifMatch: "W/\"1\""), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(otherFlow.ReleaseAsync(id, ifMatch: "W/\"1\""), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(otherFlow.SyncAsync(id, ifMatch: "W/\"1\""), 404)));
        Assert.Equal("WMS_PACKAGE_NOT_FOUND", Code(await Expect(otherFlow.ClosePackageAsync(packageId, OutboundFlow.ETag(package)), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(otherFlow.FrozenAsync(id), 404)));

        // the same tenant, ANOTHER company (the token's company claim) → 404 on every DL-5 path (sabotage (ct): the company predicate dropped → red)
        var otherCompany = Diten.InventoryService.Tests.TestInfrastructure.TestTokens.Mint(s.Tenant, Guid.NewGuid(), s.User,
            [OutboundScenario.OutboundRead, OutboundScenario.OutboundUpdate, OutboundFlow.OutboundCancel, OutboundFlow.OutboundRelease, OutboundFlow.PackagesRead,
             OutboundFlow.PackagesUpdate, OutboundFlow.PickRead, OutboundFlow.PickConfirm]);
        var pickTask = (await f.PickTasksAsync(id)).Single();
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}/packages", otherCompany), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(f.CancelAsync(id, ifMatch: "W/\"1\"", token: otherCompany), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(f.ReleaseAsync(id, ifMatch: "W/\"1\"", token: otherCompany), 404)));
        Assert.Equal("WMS_PACKAGE_NOT_FOUND", Code(await Expect(f.ClosePackageAsync(packageId, OutboundFlow.ETag(package), token: otherCompany), 404)));
        Assert.Equal("WMS_TASK_NOT_FOUND", Code(await Expect(s.P.W.GetAsync($"/api/warehouse/pick-tasks/{PutawayScenario.TaskId(pickTask)}", otherCompany), 404)));
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}", otherCompany), 404)));

        // the company selector must match the token
        var mismatch = await Expect(s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}/packages", f.Supervisor,
            r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", Guid.NewGuid().ToString("D"))), 403);
        Assert.Equal("CROSS_LE_FORBIDDEN", Code(mismatch));

        // no token → 401; a token without the action's key → 403 (each DL-5 action)
        Assert.Equal(401, (int)(await s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}/packages", token: string.Empty)).StatusCode);
        var readOnly = s.TokenFor(OutboundScenario.OutboundRead);
        Assert.Equal(403, (int)(await f.CancelAsync(id, token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await f.ReleaseAsync(id, token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await f.SyncAsync(id, token: s.TokenFor(OutboundScenario.OutboundRead, OutboundFlow.OutboundCancel))).StatusCode);
        Assert.Equal(403, (int)(await f.CreatePackageAsync(id, OutboundFlow.Contents(), token: s.TokenFor(OutboundFlow.PackagesRead))).StatusCode);
        Assert.Equal(403, (int)(await f.ClosePackageAsync(packageId, OutboundFlow.ETag(package), token: s.TokenFor(OutboundFlow.PackagesCreate))).StatusCode);
        Assert.Equal(403, (int)(await s.P.W.GetAsync($"/api/warehouse/pick-tasks?outboundId={id}", s.TokenFor(OutboundFlow.PackagesRead))).StatusCode);
        Assert.Equal(403, (int)(await s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}/packages", s.TokenFor(OutboundFlow.PickRead))).StatusCode);
        Assert.Equal(403, (int)(await s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}", s.TokenFor(OutboundFlow.PickRead))).StatusCode);
    }

    [Fact]
    public async Task AC73_cancel_and_release_replay_once_and_another_body_is_400()
    {
        var (s, f, _, _) = await PickTaskTests.FS2Async(_factory);
        var packed = await f.PackedAsync(s.Line(10));
        var etag = await f.ETagOfOutboundAsync(packed);
        var key = NewKey();
        await Expect(f.ReleaseAsync(packed, ifMatch: etag, key: key), 200);
        await Expect(f.ReleaseAsync(packed, ifMatch: etag, key: key), 200);                                // replay (If-Match is stale now — the key first)
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Code(await Expect(f.ReleaseAsync(packed, [(1, "Baska bir gerekce")], etag, key), 400)));
        Assert.Single(f.OutboundEventKinds(packed), k => k == "Released");
        Assert.Single(f.Outbox(packed));
        Assert.Equal(1, s.Central("ReleaseOutboundShipmentCommand").Forwarded);

        var allocated = OutboundScenario.Id(await s.CreatedAsync(s.Line(10)));
        var cancelEtag = await f.ETagOfOutboundAsync(allocated);
        var cancelKey = NewKey();
        await Expect(f.CancelAsync(allocated, ifMatch: cancelEtag, key: cancelKey), 200);
        await Expect(f.CancelAsync(allocated, ifMatch: cancelEtag, key: cancelKey), 200);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Code(await Expect(f.CancelAsync(allocated, "Baska bir iptal gerekcesi", cancelEtag, cancelKey), 400)));
        Assert.Single(f.OutboundEventKinds(allocated), k => k == "Cancelled");
        Assert.Equal(1, s.Central("CancelOutboundShipmentCommand").Forwarded);
        Assert.Equal("WMS_CONFLICT", Code(await Expect(f.ReleaseAsync(await f.PackedAsync(s.Line(5)), ifMatch: "W/\"99\""), 409)));
    }
}
