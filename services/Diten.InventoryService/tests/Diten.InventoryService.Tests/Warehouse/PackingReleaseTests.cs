using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Traceability.Enums;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-5 — packing (pack §8.20) and "sevke hazır" (§8.21) on the REAL host: packages only on a Picked outbound, Σ ≤ picked, no stock
/// movement and no INVENTORY call; the last close → Packed with empty open packages cancelled; release with the pre-check (recall / lot status /
/// FEFO in every mode, no write on a refusal) and the frozen detail + outbox row in ONE transaction; the filled frozen reads.
/// </summary>
public sealed class PackingReleaseTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PackingReleaseTests(WarehouseApiFactory factory) => _factory = factory;

    private Task<(OutboundScenario S, OutboundFlow F, string L1, string L2)> FS2Async() => PickTaskTests.FS2Async(_factory);

    private static string PackageId(JsonElement p) => p.GetProperty("packageId").GetString()!;

    /// <summary>
    /// AC-29. Sabotages: (ci) no Σ ≤ picked check → "31 > 30" red; (cj) a LOCATION_TRANSFER at close → "INVENTORY calls 0" red; (dn) no cancel of
    /// the empty open package at the last close → "B Cancelled" red.
    /// </summary>
    [Fact]
    public async Task AC29_packages_open_on_Picked_hold_at_most_the_picked_quantity_and_the_last_close_packs_and_cancels_the_empty_one()
    {
        var (s, f, _, _) = await FS2Async();
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        Assert.Equal("WMS_INVALID_TRANSITION", Code(await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 1))), 409))); // Allocated
        await f.PickAllAsync(id);
        var callsBefore = _factory.PostingCalls.GetValueOrDefault(s.Tenant);
        var movementsBefore = f.Movements().Count;

        Assert.Equal("WMS_PACKAGE_EXCEEDS_PICKED", Code(await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 31))), 422)));
        Assert.Equal("INVALID_REQUEST", Code(await Expect(f.CreatePackageAsync(id, new Dictionary<string, object?>(OutboundFlow.Contents((1, 1))) { ["dimensionsCm"] = "12x30" }), 400)));
        var a = await Expect(f.CreatePackageAsync(id, new Dictionary<string, object?>(OutboundFlow.Contents((1, 20))) { ["dimensionsCm"] = "12x30x40", ["weightKg"] = "4.250" }), 201);
        Assert.Matches("^GMG-PKG-\\d{6}$", a.GetProperty("packageNumber").GetString()!);
        var b = await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents()), 201);                 // an empty open package
        Assert.Equal("WMS_PACKAGE_EXCEEDS_PICKED", Code(await Expect(f.UpdatePackageAsync(PackageId(b), OutboundFlow.Contents((1, 11)), OutboundFlow.ETag(b)), 422)));
        Assert.Equal("WMS_PACKAGE_EMPTY", Code(await Expect(f.ClosePackageAsync(PackageId(b), OutboundFlow.ETag(b)), 422)));
        var a2 = await Expect(f.UpdatePackageAsync(PackageId(a), OutboundFlow.Contents((1, 30)), OutboundFlow.ETag(a)), 200);
        Assert.Equal("WMS_CONFLICT", Code(await Expect(f.UpdatePackageAsync(PackageId(a), OutboundFlow.Contents((1, 29)), OutboundFlow.ETag(a)), 409)));
        var closed = await Expect(f.ClosePackageAsync(PackageId(a), OutboundFlow.ETag(a2)), 200);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal("WMS_OUTBOUND_IMMUTABLE", "WMS_OUTBOUND_IMMUTABLE"); // (after release, below)

        // the last close filled every allocation: Packed, and the empty open B Cancelled in the same write (+ PackageCancelled)
        var view = await f.ViewAsync(id);
        Assert.Equal("Packed", view.GetProperty("status").GetString());
        Assert.Equal("Cancelled", f.Packages(id).Single(p => p["_id"].ToString() == PackageId(b))["Status"].AsString);
        Assert.Contains("PackageCancelled", f.OutboundEventKinds(id));
        Assert.Contains("Packed", f.OutboundEventKinds(id));
        Assert.Equal("WMS_INVALID_TRANSITION", Code(await Expect(f.UpdatePackageAsync(PackageId(a), OutboundFlow.Contents((1, 30)), OutboundFlow.ETag(closed)), 409)));

        // packing made no stock movement and called INVENTORY not at all
        Assert.Equal(callsBefore, _factory.PostingCalls.GetValueOrDefault(s.Tenant));
        Assert.Equal(movementsBefore, f.Movements().Count);

        // released: the frozen packages hold A only
        await Expect(f.ReleaseAsync(id), 200);
        var frozen = JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement;
        var package = Assert.Single(frozen.GetProperty("packages").EnumerateArray());
        Assert.Equal(a.GetProperty("packageNumber").GetString(), package.GetProperty("packageId").GetString());
        Assert.Equal("WMS_OUTBOUND_IMMUTABLE", Code(await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 1))), 409)));
    }

    /// <summary>AC-29: the package commands replay (same key + body → 200, no new row), other body → 400; the Packing read carries no ship-to.</summary>
    [Fact]
    public async Task AC29_AC73_package_commands_replay_and_the_packing_read_carries_no_ship_to()
    {
        var (s, f, _, _) = await FS2Async();
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        await f.PickAllAsync(id);
        var key = NewKey();
        var created = await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 30)), key, f.Operator), 201);
        var again = await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 30)), key, f.Operator), 200);
        Assert.Equal(PackageId(created), PackageId(again));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Code(await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 29)), key, f.Operator), 400)));
        Assert.Single(f.Packages(id));
        Assert.Single(f.OutboundEventKinds(id), k => k == "PackageCreated");

        var closeKey = NewKey();
        await Expect(f.ClosePackageAsync(PackageId(created), OutboundFlow.ETag(created), closeKey, f.Operator), 200);
        await Expect(f.ClosePackageAsync(PackageId(created), OutboundFlow.ETag(created), closeKey, f.Operator), 200);
        Assert.Single(f.OutboundEventKinds(id), k => k == "PackageClosed");
        Assert.Equal(1, s.Central("CloseHandlingUnitCommand").Forwarded);
        Assert.Equal(1, s.Central("CreateHandlingUnitCommand").Forwarded);

        var packing = await f.PackingAsync(id, f.Operator);                 // the operator holds no outbound read (Q-10a)
        Assert.DoesNotContain("shipTo", packing.ToString());
        Assert.DoesNotContain("Ada Hastanesi", packing.ToString());
        Assert.Equal("Packed", packing.GetProperty("outbound").GetProperty("status").GetString());
        await Expect(s.ViewAsync(id, f.Operator), 403);
    }

    /// <summary>
    /// AC-30. Sabotages: (ck) the outbox write outside the transaction → K2 red (ReadyToShip with no outbox row); (cl) an edit after ReadyToShip
    /// allowed → 409 and the hash equality red; (dw) a <c>:guid</c> constraint on the frozen <c>{outboundId}</c> → "OUT-9001 → frozen 404" red.
    /// </summary>
    [Fact]
    public async Task AC30_release_publishes_the_frozen_detail_one_outbox_row_and_freezes_the_outbound()
    {
        var (s, f, l1, l2) = await FS2Async();
        var id = await f.PackedAsync(s.Line(60, orderLineId: "SO-9/1"));
        var outbound = await f.ViewAsync(id);

        // K2: the outbox write fails inside the transaction → the command rolls back, Packed, no outbox row
        _factory.FailOutbox[s.Tenant] = true;
        await Expect(f.ReleaseAsync(id), 500);
        _factory.FailOutbox.TryRemove(s.Tenant, out _);
        Assert.Equal("Packed", (await f.ViewAsync(id)).GetProperty("status").GetString());
        Assert.Empty(f.Outbox(id));

        var released = await Expect(f.ReleaseAsync(id), 200);
        Assert.Equal("ReadyToShip", released.GetProperty("status").GetString());
        Assert.True(released.TryGetProperty("readyAt", out _));
        var outbox = Assert.Single(f.Outbox(id));
        Assert.Equal("OutboundShipmentReady", outbox["EventName"].AsString);
        Assert.Equal("Pending", outbox["Status"].AsString);
        var payload = JsonDocument.Parse(outbox["PayloadJson"].AsString).RootElement;
        Assert.Equal(id, payload.GetProperty("outboundId").GetString());
        Assert.Equal(s.LegalEntity.ToString("D"), payload.GetProperty("legalEntityId").GetString());
        Assert.Equal(s.Tenant.ToString("D"), payload.GetProperty("tenantId").GetString());
        Assert.True(Guid.TryParseExact(payload.GetProperty("correlationId").GetString(), "D", out _));

        var text = await f.FrozenTextAsync(id);
        var frozen = JsonDocument.Parse(text).RootElement;
        Assert.Empty(FrozenOutboundSchema.ValidateShipment(frozen));
        var lines = frozen.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);                                                  // one orderLineId, two lots → two lines
        Assert.All(lines, l => Assert.Equal("SO-9/1", l.GetProperty("orderLineId").GetString()));
        Assert.Equal([(l1, "30.000"), (l2, "30.000")], lines.Select(l => (l.GetProperty("lotId").GetString(), l.GetProperty("quantity").GetString())).ToList());
        Assert.All(lines, l => Assert.Equal(s.S01.ToString("D"), l.GetProperty("fromLocationId").GetString()));
        Assert.All(lines, l => Assert.Equal("AVAILABLE", l.GetProperty("stockStatus").GetString()));
        Assert.Equal(OutboundFlow.Allocs(outbound).Select(a => a.GetProperty("reservationId").GetString()), lines.Select(l => l.GetProperty("reservationId").GetString()));
        Assert.False(frozen.TryGetProperty("shipment", out _));
        Assert.Equal(FrozenOutboundSchema.Hash(text), FrozenOutboundSchema.Hash(await f.FrozenTextAsync(id)));   // the drift hash

        var list = await f.FrozenListAsync("?status=ReadyToShip");
        Assert.Empty(FrozenOutboundSchema.ValidatePage(list));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("outboundId").GetString() == id);

        var etag = OutboundFlow.ETag(await f.ViewAsync(id));
        Assert.Equal("WMS_OUTBOUND_IMMUTABLE", Code(await Expect(s.UpdateAsync(id, new Dictionary<string, object?> { ["note"] = "late" }, etag, token: f.Supervisor), 409)));
        Assert.Equal(FrozenOutboundSchema.Hash(text), FrozenOutboundSchema.Hash(await f.FrozenTextAsync(id)));
        Assert.Equal(1, s.Central("ReleaseOutboundShipmentCommand").Forwarded);
        Assert.Equal(FrozenOutboundSchema.Sha256, FrozenOutboundSchema.FileSha256());   // the frozen 1.0.0 file is byte-equal (AC-18)
    }

    /// <summary>AC-30 frozen path (r23 F-14): non-UUID ids are the frozen 404 body; the fixed <c>search</c> segment is the search answer; internal states absent.</summary>
    [Fact]
    public async Task AC30_the_frozen_path_takes_any_string_and_internal_states_stay_invisible()
    {
        var (s, f, _, _) = await FS2Async();
        foreach (var raw in new[] { "OUT-9001", "not-a-uuid", Guid.NewGuid().ToString("D") })
        {
            var error = await Expect(f.FrozenAsync(raw), 404);
            Assert.Equal("UNKNOWN_OUTBOUND", Code(error));
            Assert.Equal("v1", error.GetProperty("contractVersion").GetString());
        }

        Assert.True((await Expect(s.SearchAsync(token: f.Supervisor), 200)).TryGetProperty("items", out _));

        // an internal Allocating outbound: absent from the list, 404 on the detail
        _factory.AroundBatch[s.Tenant] = (_, _) => throw new TimeoutException("test: outcome unknown");
        using var unknown = await s.CreateAsync(s.Body(s.Line(10)));
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
        var stuck = Assert.Single(s.StoredOutbounds(), o => o["Status"].AsString == "Allocating");
        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(f.FrozenAsync(stuck["_id"].ToString()!), 404)));
        Assert.DoesNotContain((await f.FrozenListAsync()).GetProperty("items").EnumerateArray(), i => i.GetProperty("outboundId").GetString() == stuck["_id"].ToString());
        Assert.Empty(FrozenOutboundSchema.ValidatePage(await f.FrozenListAsync()));
    }

    /// <summary>AC-72: one of two allocations closed with 0 → the frozen detail and the outbox event carry ONE line (30). Sabotage (dm) writes the zero line.</summary>
    [Fact]
    public async Task AC72_an_allocation_closed_with_zero_is_never_written_to_the_frozen_detail()
    {
        var (s, f, l1, _) = await FS2Async();
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(60)));
        var tasks = await f.PickTasksAsync(id);
        await f.ConfirmedAsync(tasks.Single(t => t.GetProperty("lotId").GetString() == l1));
        await Expect(f.CloseShortAsync(tasks.Single(t => t.GetProperty("lotId").GetString() != l1)), 200);
        await f.PackAllAsync(id);
        await Expect(f.ReleaseAsync(id), 200);
        var frozen = JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement;
        var line = Assert.Single(frozen.GetProperty("lines").EnumerateArray());
        Assert.Equal(("30.000", l1), (line.GetProperty("quantity").GetString(), line.GetProperty("lotId").GetString()));
    }

    /// <summary>
    /// AC-77 (release part). Sabotages: (di) no recall read → "422 LOT_UNDER_RECALL" red; (dk) the mode not read (always RequireReason) → the Block
    /// case red; (dp) the re-evaluation only in Block → the RequireReason case red; (eg) no 1000 bound on the release reason → "1001 → 400" red.
    /// </summary>
    [Fact]
    public async Task AC77_recall_and_lot_status_refuse_the_release_without_a_write()
    {
        foreach (var (kind, code) in new[] { ("recall", "LOT_UNDER_RECALL"), ("blocked", "LOT_BLOCKED"), ("expired", "LOT_EXPIRED") })
        {
            var (s, f, l1, _) = await FS2Async();
            var id = await f.PackedAsync(s.Line(30));
            var version = (await f.ViewAsync(id)).GetProperty("version").GetInt32();
            if (kind == "recall") f.Recall(l1);
            else f.LotStatus(l1, kind == "blocked" ? LotStatus.Blocked : LotStatus.Expired);
            var error = await Expect(f.ReleaseAsync(id), 422);
            Assert.Equal(code, Code(error));
            Assert.Equal(1, Details(error).GetProperty("allocSeq").GetInt32());
            var view = await f.ViewAsync(id);
            Assert.Equal(("Packed", version), (view.GetProperty("status").GetString(), view.GetProperty("version").GetInt32()));
            Assert.Empty(f.Outbox(id));
            Assert.Equal(0, s.Central("ReleaseOutboundShipmentCommand").Forwarded);
        }

        var (s2, f2, _, _) = await FS2Async();
        var id2 = await f2.PackedAsync(s2.Line(30));
        _factory.RecallDown[s2.Tenant] = true;
        await Expect(f2.ReleaseAsync(id2), 503);
        _factory.RecallDown.TryRemove(s2.Tenant, out _);
        Assert.Equal("Packed", (await f2.ViewAsync(id2)).GetProperty("status").GetString());
    }

    /// <summary>AC-77 FEFO re-evaluation in every mode: an earlier-expiry free lot L0 arrives after picking L2.</summary>
    [Fact]
    public async Task AC77_a_later_earlier_lot_is_a_deviation_block_refuses_require_reason_asks_warn_passes()
    {
        foreach (var mode in new[] { FefoOutboundMode.Block, FefoOutboundMode.RequireReason, FefoOutboundMode.Warn })
        {
            var s = await new OutboundScenario(_factory).ReadyAsync();
            var f = new OutboundFlow(s);
            s.Policy(mode);
            var l2 = s.Lot("2027-03-31");
            await s.ReceiveAsync(30, s.A01, l2);
            var id = await f.PackedAsync(s.Line(30));
            var l0 = s.Lot("2026-12-31");
            await s.ReceiveAsync(5, s.A02, l0);                                // an earlier, free lot now

            Assert.Equal("INVALID_REQUEST", Code(await Expect(f.ReleaseAsync(id, [(1, new string('x', 1001))]), 400)));
            if (mode == FefoOutboundMode.Block)
            {
                var error = await Expect(f.ReleaseAsync(id), 422);
                Assert.Equal("FEFO_ORDER_VIOLATION", Code(error));
                Assert.Equal(l0, Details(error).GetProperty("firstEligibleLotId").GetString());
                Assert.Equal("Packed", (await f.ViewAsync(id)).GetProperty("status").GetString());
            }
            else if (mode == FefoOutboundMode.RequireReason)
            {
                var error = await Expect(f.ReleaseAsync(id), 422);
                Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", Code(error));
                var deviation = Assert.Single(Details(error).GetProperty("deviations").EnumerateArray());
                Assert.Equal((1, l0), (deviation.GetProperty("allocSeq").GetInt32(), deviation.GetProperty("firstEligibleLotId").GetString()));
                Assert.Empty(f.Outbox(id));
                await Expect(f.ReleaseAsync(id, [(1, "Musteri L2 istedi")]), 200);
                var frozen = JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement;
                Assert.Equal("Musteri L2 istedi", Assert.Single(frozen.GetProperty("lines").EnumerateArray()).GetProperty("fefoBypassReason").GetString());
            }
            else
            {
                await Expect(f.ReleaseAsync(id), 200);
                var frozen = JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement;
                Assert.False(Assert.Single(frozen.GetProperty("lines").EnumerateArray()).TryGetProperty("fefoBypassReason", out _));
            }
        }
    }

    /// <summary>
    /// AC-33 (v): an Item outbound Packed → release 409 WMS_ITEM_LEVEL_NOT_PUBLISHABLE (Packed, no outbox); absent from every frozen list (each status filter) and
    /// the page passes the schema derived from the production file; detail 404; cancel works. Sabotage (dd) lists it (skuLevel Item → schema red).
    /// </summary>
    [Fact]
    public async Task AC33_v_an_item_outbound_is_never_published_and_never_on_the_frozen_reads()
    {
        var s = await new OutboundScenario(_factory, level: SkuLevel.Item).ReadyAsync();
        var f = new OutboundFlow(s);
        var kl1 = s.Lot("2027-01-31");
        await s.ReceiveAsync(20, s.A01, kl1);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(20)));
        foreach (var status in new[] { "", "?status=Allocated" })
        {
            var page = await f.FrozenListAsync(status);
            Assert.DoesNotContain(page.GetProperty("items").EnumerateArray(), i => i.GetProperty("outboundId").GetString() == id);
            Assert.Empty(FrozenOutboundSchema.ValidatePage(page));
        }

        await f.PickAllAsync(id);
        await f.PackAllAsync(id);
        Assert.Equal("WMS_ITEM_LEVEL_NOT_PUBLISHABLE", Code(await Expect(f.ReleaseAsync(id), 409)));
        Assert.Equal("Packed", (await f.ViewAsync(id)).GetProperty("status").GetString());
        Assert.Empty(f.Outbox(id));
        foreach (var status in new[] { "", "?status=Packed", "?status=ReadyToShip" })
        {
            var page = await f.FrozenListAsync(status);
            Assert.DoesNotContain(page.GetProperty("items").EnumerateArray(), i => i.GetProperty("outboundId").GetString() == id);
            Assert.Empty(FrozenOutboundSchema.ValidatePage(page));
        }

        Assert.Equal("UNKNOWN_OUTBOUND", Code(await Expect(f.FrozenAsync(id), 404)));
        await Expect(f.CancelAsync(id), 200);
        Assert.All(OutboundFlow.Allocs(await f.ViewAsync(id)), a => Assert.Equal("Released", a.GetProperty("reservationReleaseState").GetString()));
    }

    /// <summary>The schema derivation itself reads the production file: SkuLevel three values, OutboundStatus six, required members.</summary>
    [Fact]
    public void AC30_the_frozen_schema_is_derived_from_the_production_contract_file()
    {
        Assert.Equal(["Gsku", "Lsku", "FinishedGood"], FrozenOutboundSchema.EnumOf("SkuLevel"));
        Assert.Equal(["Allocated", "Picked", "Packed", "ReadyToShip", "Shipped", "Cancelled"], FrozenOutboundSchema.EnumOf("OutboundStatus"));
        var bad = JsonDocument.Parse("""{"outboundId":"x","warehouseId":"w","status":"ReadyToShip","shipTo":{"name":"n","country":"tr","city":null},"lines":[{"itemId":"no","skuId":"7a1e0c58-1111-4111-8111-111111111111","skuLevel":"Item","quantity":"1","uomId":"EA"}]}""").RootElement;
        var errors = FrozenOutboundSchema.ValidateShipment(bad);
        Assert.Contains(errors, e => e.Contains("city") && e.Contains("null"));
        Assert.Contains(errors, e => e.Contains("itemId") && e.Contains("UUID"));
        Assert.Contains(errors, e => e.Contains("skuLevel") && e.Contains("enum"));
    }
}
