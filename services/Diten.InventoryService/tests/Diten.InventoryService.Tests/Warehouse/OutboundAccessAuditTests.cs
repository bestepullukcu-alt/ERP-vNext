using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-4 AC-73 (outbound paths), AC-75 (configuration part) and AC-76 (e) (0178 side of Q-10a): tenant / company isolation (404), the
/// company selector mismatch (403 <c>CROSS_LE_FORBIDDEN</c>), RBAC (401 / 403 per action; the personal-data reads need
/// <c>warehouse.outbound-shipments.read</c>, which a Viewer-like token without it never passes), the request key before If-Match (200 replay, no new
/// event; another body 400; stale If-Match 409), the state × command rules of the header edit, the central / local counts of §21, the Staging
/// rules and the base unit; and the S1 putaway surface never sees a pick task.
/// </summary>
public sealed class OutboundAccessAuditTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundAccessAuditTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<(OutboundScenario S, System.Text.Json.JsonElement Outbound)> AllocatedAsync()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var lot = s.Lot("2027-01-31");
        await s.ReceiveAsync(30, s.A01, lot);
        return (s, await s.CreatedAsync(s.Line(10)));
    }

    /// <summary>Sabotage (ct): the outbound store without its LegalEntityId predicate → another company's outbound answers 200 (red).</summary>
    [Fact]
    public async Task AC73_another_tenant_or_company_gets_404_and_a_selector_mismatch_403()
    {
        var (s, outbound) = await AllocatedAsync();
        var id = OutboundScenario.Id(outbound);
        var other = await new OutboundScenario(_factory).ReadyAsync();                                    // another tenant
        var otherCompany = TestTokens.Mint(s.Tenant, Guid.NewGuid(), s.User, [OutboundRead, OutboundCreate, OutboundUpdate]);

        foreach (var token in new[] { other.Planner, otherCompany })
        {
            Assert.Equal("UNKNOWN_OUTBOUND", await ErrorCode(s.ViewAsync(id, token), 404));
            Assert.Equal("UNKNOWN_OUTBOUND", await ErrorCode(s.EventsAsync(id, token), 404));
            Assert.Equal("UNKNOWN_OUTBOUND", await ErrorCode(s.UpdateAsync(id, new() { ["note"] = "x" }, ETag(outbound), token: token), 404));
            Assert.Equal(0, (await Expect(s.SearchAsync(token: token), 200)).GetProperty("total").GetInt64());
        }

        var mismatch = await Expect(s.P.W.GetAsync($"/api/warehouse/outbound-shipments/{id}/view", s.Planner,
            r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", Guid.NewGuid().ToString("D"))), 403);
        Assert.Equal("CROSS_LE_FORBIDDEN", Code(mismatch));
        Assert.Equal("UNKNOWN_OUTBOUND", await ErrorCode(s.ViewAsync(Guid.NewGuid().ToString("D")), 404));
        Assert.Equal("UNKNOWN_OUTBOUND", await ErrorCode(s.ViewAsync("OUT-9001"), 404));
    }

    /// <summary>AC-73 RBAC + AC-76 (e): no token 401; a token without the action's key 403; a Viewer-like token (every other read) cannot read outbounds.</summary>
    [Fact]
    public async Task AC73_AC76e_every_action_needs_its_key_and_the_personal_data_reads_need_outbound_read()
    {
        var (s, outbound) = await AllocatedAsync();
        var id = OutboundScenario.Id(outbound);
        var viewer = s.TokenFor(PutawayScenario.TaskRead, Read, "warehouse.pick-tasks.read", "warehouse.packages.read", "warehouse.exceptions.read",
            "warehouse.activity.read", "warehouse.integration-monitor.read");
        var reader = s.TokenFor(OutboundRead);

        Assert.Equal(403, (int)(await s.SearchAsync(token: viewer)).StatusCode);
        Assert.Equal(403, (int)(await s.ViewAsync(id, viewer)).StatusCode);
        Assert.Equal(403, (int)(await s.EventsAsync(id, viewer)).StatusCode);
        Assert.Equal(200, (int)(await s.SearchAsync(token: reader)).StatusCode);
        Assert.Equal(200, (int)(await s.ViewAsync(id, reader)).StatusCode);
        Assert.Equal(403, (int)(await s.CreateAsync(s.Body(s.Line(1)), reader)).StatusCode);
        Assert.Equal(403, (int)(await s.PreviewAsync(s.Body(s.Line(1)), reader)).StatusCode);
        Assert.Equal(403, (int)(await s.UpdateAsync(id, new() { ["note"] = "x" }, ETag(outbound), token: s.TokenFor(OutboundRead, OutboundCreate))).StatusCode);
        Assert.Equal(401, (int)(await s.SearchAsync(token: string.Empty)).StatusCode);
        Assert.Equal(401, (int)(await s.CreateAsync(s.Body(s.Line(1)), string.Empty)).StatusCode);
    }

    /// <summary>
    /// AC-73 replay / If-Match / audit for the header edit: key before If-Match (the same request with its first If-Match → 200, no new event),
    /// another body → 400, stale → 409 WMS_CONFLICT; central 1 per successful edit, 0 for a replay or a refusal; the ship-to only as a mark.
    /// </summary>
    [Fact]
    public async Task AC73_the_header_edit_replays_before_if_match_and_counts_once()
    {
        var (s, outbound) = await AllocatedAsync();
        var id = OutboundScenario.Id(outbound);
        var change = new Dictionary<string, object?>
        {
            ["orderRef"] = "SO-2026-0002", ["note"] = "kapı 3",
            ["shipTo"] = new Dictionary<string, object?> { ["name"] = "Ada Hastanesi Ek Bina", ["country"] = "tr" }
        };
        var key = NewKey();

        var version = outbound.GetProperty("version").GetInt32();                                     // 2: transaction A, then B
        var edited = await Expect(s.UpdateAsync(id, change, ETag(outbound), key), 200);
        Assert.Equal(version + 1, edited.GetProperty("version").GetInt32());
        Assert.Equal("SO-2026-0002", edited.GetProperty("orderRef").GetString());
        var events = s.OutboundEvents(id).Count;

        var replay = await Expect(s.UpdateAsync(id, change, ETag(outbound), key), 200);                // the first If-Match: still a replay
        Assert.Equal(version + 1, replay.GetProperty("version").GetInt32());
        Assert.Equal(events, s.OutboundEvents(id).Count);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.UpdateAsync(id, new() { ["note"] = "other" }, ETag(edited), key), 400));
        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.UpdateAsync(id, new() { ["note"] = "stale" }, ETag(outbound)), 409));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.UpdateAsync(id, new() { ["lines"] = Array.Empty<object>() }, ETag(edited)), 400));

        Assert.Equal((1, 0), s.Central("UpdateOutboundShipmentCommand"));
        var updated = Assert.Single(s.OutboundEvents(id), e => e["Kind"].AsString == "Updated");
        Assert.Equal("true", updated["Data"]["shipToChanged"].AsString);
        Assert.Equal("SO-2026-0001", updated["Data"]["orderRefBefore"].AsString);
        Assert.DoesNotContain("Ada Hastanesi", s.OutboundEvents(id).Select(e => e["Data"].ToJson()).Aggregate(string.Empty, string.Concat));
        Assert.Equal((1, 0), s.Central("CreateOutboundShipmentCommand"));
    }

    [Fact]
    public async Task AC73_the_create_replays_with_no_new_event_and_another_body_is_400()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var lot = s.Lot("2027-01-31");
        await s.ReceiveAsync(30, s.A01, lot);
        var body = s.Body(s.Line(10));

        var created = await Expect(s.CreateAsync(body), 201);
        var events = s.OutboundEvents().Count;
        var replay = await Expect(s.CreateAsync(body), 200);

        Assert.Equal(OutboundScenario.Id(created), OutboundScenario.Id(replay));
        Assert.Equal(events, s.OutboundEvents().Count);
        Assert.Single(s.StoredOutbounds());
        Assert.Single(s.Reservations());
        Assert.Equal((1, 0), s.Central("CreateOutboundShipmentCommand"));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateAsync(new Dictionary<string, object?>(body) { ["orderRef"] = "X" }), 400));
    }

    /// <summary>§8.18: ReadyToShip / Shipped → 409 WMS_OUTBOUND_IMMUTABLE; Cancelled → 409 WMS_INVALID_TRANSITION; an internal state → 404.</summary>
    [Theory]
    [InlineData(OutboundStatus.ReadyToShip, 409, "WMS_OUTBOUND_IMMUTABLE")]
    [InlineData(OutboundStatus.Shipped, 409, "WMS_OUTBOUND_IMMUTABLE")]
    [InlineData(OutboundStatus.Cancelled, 409, "WMS_INVALID_TRANSITION")]
    [InlineData(OutboundStatus.Allocating, 404, "UNKNOWN_OUTBOUND")]
    [InlineData(OutboundStatus.AllocationFailed, 404, "UNKNOWN_OUTBOUND")]
    public async Task AC73_the_header_edit_follows_the_state_table(OutboundStatus status, int code, string error)
    {
        var (s, outbound) = await AllocatedAsync();
        var id = OutboundScenario.Id(outbound);
        var outbounds = _factory.Database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments);
        await outbounds.UpdateOneAsync(o => o.Id == Guid.Parse(id), Builders<OutboundShipment>.Update.Set(o => o.Status, status));

        Assert.Equal(error, await ErrorCode(s.UpdateAsync(id, new() { ["note"] = "x" }, ETag(outbound)), code));
        Assert.Empty(s.CentralRows("UpdateOutboundShipmentCommand"));
        if (status is OutboundStatus.Allocating or OutboundStatus.AllocationFailed)
        {
            Assert.Equal("UNKNOWN_OUTBOUND", await ErrorCode(s.ViewAsync(id), 404));
            Assert.Equal(0, (await Expect(s.SearchAsync(), 200)).GetProperty("total").GetInt64());
        }
    }

    [Fact]
    public async Task AC73_the_search_and_events_read_the_company_s_outbounds()
    {
        var (s, outbound) = await AllocatedAsync();
        var id = OutboundScenario.Id(outbound);

        var page = await Expect(s.SearchAsync("&status=Allocated&q=GMG-OUT"), 200);
        Assert.Equal(id, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("outboundId").GetString());
        Assert.Equal(0, (await Expect(s.SearchAsync("&status=Picked"), 200)).GetProperty("total").GetInt64());
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.SearchAsync("&status=Allocating"), 400));
        var events = await Expect(s.EventsAsync(id), 200);
        Assert.Equal(["Created", "Allocated"], events.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("kind").GetString()));
    }

    /// <summary>DL-4: a pick task shares wms_tasks but the S1 putaway surface never sees it (404) — no putaway action can run on it.</summary>
    [Fact]
    public async Task DL4_a_pick_task_is_unknown_to_the_putaway_paths()
    {
        var (s, outbound) = await AllocatedAsync();
        var pick = Assert.Single(s.PickTasks(OutboundScenario.Id(outbound)));
        var taskId = pick["_id"].ToString()!;

        Assert.Equal("WMS_TASK_NOT_FOUND", await ErrorCode(s.P.W.GetAsync($"/api/warehouse/putaway-tasks/{taskId}", s.P.Supervisor), 404));
        Assert.Equal("WMS_TASK_NOT_FOUND", await ErrorCode(s.P.ConfirmRawAsync(taskId, s.P.ConfirmBody(1, s.A02), "W/\"1\""), 404));
        Assert.DoesNotContain((await Expect(s.P.W.GetAsync("/api/warehouse/putaway-tasks?pageSize=200", s.P.Supervisor), 200)).GetProperty("items").EnumerateArray(),
            t => t.GetProperty("taskId").GetString() == taskId);
    }

    // ── AC-75 (configuration part) ──────────────────────────────────────────────────────────────────────

    /// <summary>Sabotage (cx-c): the Staging check removed (any stockable node accepted) → the "no Staging" outbound opens (red).</summary>
    [Fact]
    public async Task AC75_staging_must_be_configured_unambiguous_and_a_staging_node()
    {
        var none = new OutboundScenario(_factory);
        await none.P.ReadyAsync();                                                                 // Receiving only, no Staging role
        var lot = none.Lot("2027-01-31");
        await none.ReceiveAsync(10, none.A01, lot);
        Assert.Equal("WMS_STAGING_NOT_CONFIGURED", await ErrorCode(none.CreateAsync(none.Body(none.Line(5))), 422));

        var s = await new OutboundScenario(_factory).ReadyAsync();
        var lot2 = s.Lot("2027-01-31");
        await s.ReceiveAsync(10, s.A01, lot2);
        var secondZone = s.P.W.Node(LocationType.Zone);
        var s02 = s.P.W.Node(LocationType.Bin, secondZone);
        await s.P.W.CreatedAsync(secondZone.Id, ["Staging"]);

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Body(s.Line(5))), 400));
        var notStaging = s.Body(s.Line(5));
        notStaging["stagingLocationId"] = s.A02.ToString("D");
        var refused = await Expect(s.CreateAsync(notStaging), 422);
        Assert.Equal("WMS_TARGET_NOT_ALLOWED", Code(refused));
        Assert.Equal("NotStaging", Details(refused).GetProperty("rule").GetString());

        var chosen = s.Body(s.Line(5));
        chosen["stagingLocationId"] = s02.Id.ToString("D");                                       // the inherited Staging of the second zone
        var outbound = await Expect(s.CreateAsync(chosen), 201);
        Assert.Equal(s02.Id.ToString("D"), outbound.GetProperty("stagingLocationId").GetString());
        Assert.Equal(s02.Id.ToString("D"), Assert.Single(s.PickTasks(OutboundScenario.Id(outbound)))["ToLocationId"].ToString());
        Assert.Empty(none.StoredOutbounds());
    }

    [Fact]
    public async Task AC75_the_line_unit_must_be_the_base_unit()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var lot = s.Lot("2027-01-31");
        await s.ReceiveAsync(10, s.A01, lot);

        var body = s.Body(Line("SO/1", 5, s.Item, s.Sku, SkuLevel.Lsku, uom: "BOX"));
        var refused = await Expect(s.CreateAsync(body), 422);

        Assert.Equal("WMS_UOM_NOT_BASE", Code(refused));
        Assert.Empty(s.StoredOutbounds());
    }

    /// <summary>§8.16 / AC-75: a serial-controlled SKU is picked whole — an allocation that would take a serial bucket partly → 422.</summary>
    [Fact]
    public async Task AC75_a_partial_serial_bucket_is_refused()
    {
        var s = await new OutboundScenario(_factory, serialControlled: true).ReadyAsync();
        var lot = s.Lot("2027-01-31");
        var receipt = s.P.Inventory.Receipt("3", location: s.A01.ToString("D"));
        receipt["lotId"] = lot;
        receipt["serialIds"] = new[] { "SN-" + Guid.NewGuid().ToString("N")[..6], "SN-" + Guid.NewGuid().ToString("N")[..6], "SN-" + Guid.NewGuid().ToString("N")[..6] };
        await s.P.Inventory.PostExpectingAsync(receipt, 201);

        Assert.Equal("WMS_SERIAL_PARTIAL_NOT_SUPPORTED", await ErrorCode(s.CreateAsync(s.Body(s.Line(2))), 422));
        Assert.Empty(s.StoredOutbounds());
        var whole = await s.CreatedAsync(s.Line(3));
        Assert.Equal([(s.A01, 3m)], Tasks(whole));
    }
}
