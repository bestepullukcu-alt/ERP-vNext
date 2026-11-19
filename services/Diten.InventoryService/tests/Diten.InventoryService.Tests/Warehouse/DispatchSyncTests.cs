using System.Net;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Rules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-5 — "Sevkiyatı kontrol et" (pack §8.21; AC-31, AC-78 (i)–(iii)) through the REAL MOD-0177 reader (<c>ShipmentBundleHttpReader</c>,
/// DI unchanged) and a fake SHIPMENT-BUNDLE behind its HTTP client: the dispatched decision on the MVP-6 status, the source check of every
/// candidate (one result too), the read-error mapping (403 / 503, never "no shipment"), Shipped with the shipment link (on /view only), the
/// post-dispatch reservation release and the short-dispatch put-back / unresolved rows. Fixture: L1 30 picked to S-01, ReadyToShip.
/// </summary>
public sealed class DispatchSyncTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public DispatchSyncTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<(OutboundScenario S, OutboundFlow F, string L1, string Id)> ReadyAsync()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var id = await f.ReadyAsync(s.Line(30));
        return (s, f, l1, id);
    }

    private static async Task<(int Version, int Events)> StateAsync(OutboundFlow f, string id)
        => ((await f.ViewAsync(id)).GetProperty("version").GetInt32(), f.S.OutboundEvents(id).Count);

    /// <summary>AC-31: Planned / Draft / izsiz Cancelled → 200 dispatchSeen false, no write, no local row, no central record. Sabotage (cm) ships on Planned.</summary>
    [Fact]
    public async Task AC31_planned_draft_and_traceless_cancelled_change_nothing()
    {
        foreach (var (status, issued) in new[] { ("Planned", (bool?)null), ("Draft", null), ("Cancelled", false) })
        {
            var (s, f, l1, id) = await ReadyAsync();
            f.Mvp6.Items.Add(new FakeShipment { Status = status, StockIssued = issued, Lines = [(s.Sku, l1, s.S01, 30m)] });
            var before = await StateAsync(f, id);
            var answer = await Expect(f.SyncAsync(id), 200);
            Assert.False(answer.GetProperty("dispatchSeen").GetBoolean());
            Assert.Equal(before, await StateAsync(f, id));
            Assert.Equal("ReadyToShip", (await f.ViewAsync(id)).GetProperty("status").GetString());
            Assert.Equal(0, s.Central("SyncOutboundDispatchCommand").Forwarded);
            Assert.Equal(1, f.Mvp6.DetailCalls);                                         // one detail read per candidate (one result too)
        }
    }

    /// <summary>
    /// AC-31: every dispatched status (and Cancelled with stockIssued: true) → Shipped, the link from the DETAIL, /view's shipment, the frozen hash
    /// differs only by status, the reservations released (FU03 RELEASED), central 1, the caller's Authorization and company forwarded. Sabotage (dh)
    /// treats Exception as not seen (or ignores stockIssued) → those fixtures red.
    /// </summary>
    [Fact]
    public async Task AC31_every_dispatched_status_ships_and_releases_the_remaining_reservations()
    {
        foreach (var (status, issued) in new[] { ("Dispatched", (bool?)true), ("InTransit", true), ("Delivered", true), ("Exception", true), ("Closed", true), ("Cancelled", true) })
        {
            var (s, f, l1, id) = await ReadyAsync();
            var shipment = new FakeShipment { Status = status, StockIssued = issued, ShipmentNumber = "SHP-2026-000777", Lines = [(s.Sku, l1, s.S01, 30m)] };
            f.Mvp6.Items.Add(shipment);
            var frozenBefore = await f.FrozenTextAsync(id);

            var answer = await Expect(f.SyncAsync(id), 200);

            Assert.True(answer.GetProperty("dispatchSeen").GetBoolean());
            var view = answer.GetProperty("outbound");
            Assert.Equal("Shipped", view.GetProperty("status").GetString());
            var link = view.GetProperty("shipment");
            Assert.Equal((shipment.ShipmentId, "SHP-2026-000777", status), (link.GetProperty("shipmentId").GetString(), link.GetProperty("shipmentNumber").GetString(),
                link.GetProperty("status").GetString()));
            var frozenAfter = await f.FrozenTextAsync(id);
            Assert.False(JsonDocument.Parse(frozenAfter).RootElement.TryGetProperty("shipment", out _));
            Assert.Equal(frozenBefore.Replace("\"status\":\"ReadyToShip\"", "\"status\":\"Shipped\""), frozenAfter);
            var allocation = Assert.Single(OutboundFlow.Allocs(view));
            Assert.Equal("Released", allocation.GetProperty("reservationReleaseState").GetString());
            Assert.Equal("RELEASED", (await s.ReservationViewAsync(allocation.GetProperty("reservationId").GetString()!))!.Status);
            Assert.Contains("Shipped", f.OutboundEventKinds(id));
            Assert.Equal(1, s.Central("SyncOutboundDispatchCommand").Forwarded);
            Assert.All(f.Mvp6.Calls, c => Assert.Equal(s.LegalEntity.ToString("D"), c.LegalEntity));
            Assert.All(f.Mvp6.Calls, c => Assert.StartsWith("Bearer ", c.Authorization));
            Assert.Equal(1, f.Mvp6.DetailCalls);
        }
    }

    /// <summary>AC-31 (r22): one Dispatched result whose detail is a FOREIGN source → no change, reservation ACTIVE. Sabotage (dq) skips the detail on one result.</summary>
    [Fact]
    public async Task AC31_a_foreign_shipment_never_ships_the_outbound_even_alone()
    {
        foreach (var (module, type) in new[] { ("MOD-0183-MANUAL", "WAREHOUSE_OUTBOUND"), ("MOD-0178", "SALES_ORDER") })
        {
            var (s, f, l1, id) = await ReadyAsync();
            f.Mvp6.Items.Add(new FakeShipment { SourceModule = module, SourceType = type, Lines = [(s.Sku, l1, s.S01, 30m)] });
            var before = await StateAsync(f, id);
            Assert.False((await Expect(f.SyncAsync(id), 200)).GetProperty("dispatchSeen").GetBoolean());
            Assert.Equal(before, await StateAsync(f, id));
            var view = await f.ViewAsync(id);
            Assert.Equal("ACTIVE", (await s.ReservationViewAsync(OutboundFlow.Allocs(view).Single().GetProperty("reservationId").GetString()!))!.Status);
        }
    }

    /// <summary>
    /// AC-31 read errors (§5 mapping), each its own fixture — nothing written, ReadyToShip, the same version, no central record. Sabotages: (ed) Forbidden
    /// read as "no shipment" → the 401 / 403 fixtures 200 (red); (ee) Unavailable / NotFound read as "no shipment" → the timeout and detail-404 fixtures red;
    /// (eo) the reader's default branch (other 4xx) read as "no shipment" → the 400 / 429 fixtures red.
    /// </summary>
    [Fact]
    public async Task AC31_read_errors_are_403_or_503_and_never_no_shipment()
    {
        var cases = new (string Name, Action<OutboundFlow, OutboundScenario, string> Arrange, int Status, string Code)[]
        {
            ("query 401", (f, _, _) => f.Mvp6.QueryStatus = HttpStatusCode.Unauthorized, 403, "WMS_DISPATCH_READ_FORBIDDEN"),
            ("query 403", (f, _, _) => f.Mvp6.QueryStatus = HttpStatusCode.Forbidden, 403, "WMS_DISPATCH_READ_FORBIDDEN"),
            ("detail 403", (f, s, l) => f.Mvp6.Items.Add(new FakeShipment { DetailStatus = HttpStatusCode.Forbidden }), 403, "WMS_DISPATCH_READ_FORBIDDEN"),
            ("query 500", (f, _, _) => f.Mvp6.QueryStatus = HttpStatusCode.InternalServerError, 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE"),
            ("query 400", (f, _, _) => f.Mvp6.QueryStatus = HttpStatusCode.BadRequest, 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE"),
            ("detail 429", (f, s, l) => f.Mvp6.Items.Add(new FakeShipment { DetailStatus = HttpStatusCode.TooManyRequests }), 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE"),
            ("detail 404", (f, s, l) => f.Mvp6.Items.Add(new FakeShipment { DetailStatus = HttpStatusCode.NotFound }), 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE"),
            ("timeout", (f, _, _) => f.Mvp6.Delay = TimeSpan.FromSeconds(2.5), 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE"),
            ("connection", (f, _, _) => f.Mvp6.ConnectionFails = true, 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE"),
            ("bad json", (f, _, _) => f.Mvp6.UnreadableBody = true, 503, "WMS_DISPATCH_SOURCE_UNAVAILABLE")
        };
        var (s, f, l1, id) = await ReadyAsync();
        foreach (var (name, arrange, status, code) in cases)
        {
            f.Mvp6.Items.Clear();
            f.Mvp6.QueryStatus = null;
            f.Mvp6.Delay = null;
            f.Mvp6.ConnectionFails = false;
            f.Mvp6.UnreadableBody = false;
            arrange(f, s, l1);
            var before = await StateAsync(f, id);
            var error = await Expect(f.SyncAsync(id), status);
            Assert.True(code == Code(error), $"{name}: {Code(error)}");
            Assert.Equal(before, await StateAsync(f, id));
        }

        Assert.Equal("ReadyToShip", (await f.ViewAsync(id)).GetProperty("status").GetString());
        Assert.Equal(0, s.Central("SyncOutboundDispatchCommand").Forwarded);

        // two candidates of this outbound → 409 WMS_DISPATCH_AMBIGUOUS, nothing written
        f.Mvp6.Delay = null;
        f.Mvp6.UnreadableBody = false;
        f.Mvp6.ConnectionFails = false;
        f.Mvp6.QueryStatus = null;
        f.Mvp6.Items.Clear();
        f.Mvp6.Items.Add(new FakeShipment { Lines = [(s.Sku, l1, s.S01, 30m)] });
        f.Mvp6.Items.Add(new FakeShipment { Lines = [(s.Sku, l1, s.S01, 30m)] });
        Assert.Equal("WMS_DISPATCH_AMBIGUOUS", Code(await Expect(f.SyncAsync(id), 409)));
        Assert.Equal("ReadyToShip", (await f.ViewAsync(id)).GetProperty("status").GetString());
    }

    /// <summary>AC-31: not ReadyToShip → 409 WMS_INVALID_TRANSITION; a replay of the Shipped sync → 200, no second row.</summary>
    [Fact]
    public async Task AC31_only_ReadyToShip_syncs_and_the_sync_replays()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var packed = await f.PackedAsync(s.Line(10));
        Assert.Equal("WMS_INVALID_TRANSITION", Code(await Expect(f.SyncAsync(packed), 409)));
        await Expect(f.ReleaseAsync(packed), 200);
        f.Dispatched((s.Sku, l1, s.S01, 10m));
        var key = NewKey();
        var etag = await f.ETagOfOutboundAsync(packed);
        await Expect(f.SyncAsync(packed, etag, key), 200);
        var replay = await Expect(f.SyncAsync(packed, etag, key), 200);
        Assert.True(replay.GetProperty("dispatchSeen").GetBoolean());
        Assert.Single(f.OutboundEventKinds(packed), k => k == "Shipped");
        Assert.Equal(1, s.Central("SyncOutboundDispatchCommand").Forwarded);
    }

    /// <summary>
    /// AC-78 (i): 25 of 30 issued from S-01 → Shipped and, in the same write, a ShortDispatch putaway (S-01, L1, 5) with its Created row; the Shipped row
    /// carries shortDispatch[0].quantity 5 and an empty unresolved list; ONE central row; the task is put away by S1. Sabotages: (du) no short-dispatch
    /// computation → no task (red); (ei) no Created row of the task → red.
    /// </summary>
    [Fact]
    public async Task AC78_i_a_short_dispatch_opens_one_staging_putaway_for_the_residue()
    {
        var (s, f, l1, id) = await ReadyAsync();
        var reservationId = OutboundFlow.Allocs(await f.ViewAsync(id)).Single().GetProperty("reservationId").GetString()!;
        await f.IssueAsync(25, s.S01, l1, reservationId);                                // MVP-6's GOODS_ISSUE_SALES (fixture)
        f.Dispatched((s.Sku, l1, s.S01, 25m));

        var answer = await Expect(f.SyncAsync(id), 200);
        Assert.Equal("Shipped", answer.GetProperty("outbound").GetProperty("status").GetString());

        var task = Assert.Single(f.PutawayTasks("ShortDispatch"));
        Assert.Equal((s.S01.ToString("D"), l1, 5m), (task["FromLocationId"].ToString(), task["LotId"].AsString, task["PlannedQuantity"].ToDecimal()));
        var created = Assert.Single(s.P.TaskEvents(task["_id"].ToString()!));
        Assert.Equal(("Created", "ShortDispatch"), (created["Kind"].AsString, created["Data"]["origin"].AsString));
        var shipped = s.OutboundEvents(id).Single(e => e["Kind"].AsString == "Shipped");
        var shortDispatch = JsonDocument.Parse(shipped["Data"]["shortDispatch"].AsString).RootElement.EnumerateArray().Single();
        Assert.Equal("5.000", shortDispatch.GetProperty("quantity").GetString());
        Assert.Empty(JsonDocument.Parse(shipped["Data"]["shortDispatchUnresolved"].AsString).RootElement.EnumerateArray());
        Assert.Equal(1, s.Central("SyncOutboundDispatchCommand").Forwarded);
        Assert.Empty(s.CentralRows("CreatePutawayTaskCommand"));

        // S1 puts it away (the Staging source is allowed for this origin only)
        var putaway = await s.P.GetTaskAsync(task["_id"].ToString()!, f.Supervisor);
        await Expect(s.P.ConfirmAsync(putaway, 5, s.A01, token: f.Supervisor), 201);
        Assert.Equal(0m, await s.P.OnHandAsync(s.S01));
        // the leftover reservation (30 − 25 consumed) was released after the dispatch
        Assert.Equal("RELEASED", (await s.ReservationViewAsync(reservationId))!.Status);
    }

    /// <summary>AC-78 (ii): issued = picked → no task, no unresolved row.</summary>
    [Fact]
    public async Task AC78_ii_a_full_dispatch_opens_nothing()
    {
        var (s, f, l1, id) = await ReadyAsync();
        f.Dispatched((s.Sku, l1, s.S01, 30m));
        await Expect(f.SyncAsync(id), 200);
        Assert.Empty(f.PutawayTasks("ShortDispatch"));
        Assert.Empty(s.StoredOutbound(id)["ShortDispatchUnresolved"].AsBsonArray);
    }

    /// <summary>
    /// AC-78 (iii): an unresolvable SKU → NO task and ONE unresolved row (also on the Shipped row): lotId missing (LotMissing), fromLocationId missing /
    /// another bin (NotFromStaging), 35 issued of 30 picked (OverIssued). Sabotage (eb): only the lot looked at → the NotFromStaging fixtures open a task (red).
    /// </summary>
    [Fact]
    public async Task AC78_iii_an_unresolvable_sku_gets_one_unresolved_row_and_no_task()
    {
        var cases = new (string Reason, Func<OutboundScenario, string, (Guid, string?, Guid?, decimal)> Line, decimal Issued)[]
        {
            ("LotMissing", (s, l1) => (s.Sku, null, s.S01, 25m), 25m),
            ("NotFromStaging", (s, l1) => (s.Sku, l1, null, 25m), 25m),
            ("NotFromStaging", (s, l1) => (s.Sku, l1, s.A01, 25m), 25m),
            ("OverIssued", (s, l1) => (s.Sku, l1, s.S01, 35m), 35m)
        };
        foreach (var (reason, line, issued) in cases)
        {
            var (s, f, l1, id) = await ReadyAsync();
            f.Dispatched(line(s, l1));
            await Expect(f.SyncAsync(id), 200);
            Assert.Empty(f.PutawayTasks("ShortDispatch"));
            var row = Assert.Single(s.StoredOutbound(id)["ShortDispatchUnresolved"].AsBsonArray).AsBsonDocument;
            Assert.Equal((reason, 30m, issued), (row["Reason"].AsString, row["Expected"].ToDecimal(), row["Issued"].ToDecimal()));
            var shipped = s.OutboundEvents(id).Single(e => e["Kind"].AsString == "Shipped");
            var unresolved = JsonDocument.Parse(shipped["Data"]["shortDispatchUnresolved"].AsString).RootElement.EnumerateArray().Single();
            Assert.Equal(reason, unresolved.GetProperty("reason").GetString());
            Assert.Equal("Shipped", (await f.ViewAsync(id)).GetProperty("status").GetString());
        }
    }

    /// <summary>
    /// AC-78 (iii) "another Staging node" (DL-4 → DL-5 hand-over): the line issued from S-02, another bin of the SAME Staging zone (the role inherited) —
    /// not the outbound's StagingLocationId S-01 → NotFromStaging, no task (resolvability is the ONE node, r25 F-2). Sabotages: (eb) the location condition
    /// removed, (eb-b) only a missing location refused → a task opens (red).
    /// </summary>
    [Fact]
    public async Task AC78_iii_another_node_of_the_staging_zone_is_not_the_staging_location()
    {
        var (s, f, l1, id) = await ReadyAsync();
        var s02 = s.P.W.Node(Diten.InventoryService.Domain.Enums.LocationType.Bin, s.StagingZone);
        f.Dispatched((s.Sku, l1, s02.Id, 25m));
        await Expect(f.SyncAsync(id), 200);
        Assert.Empty(f.PutawayTasks("ShortDispatch"));
        var row = Assert.Single(s.StoredOutbound(id)["ShortDispatchUnresolved"].AsBsonArray).AsBsonDocument;
        Assert.Equal(("NotFromStaging", 30m, 25m), (row["Reason"].AsString, row["Expected"].ToDecimal(), row["Issued"].ToDecimal()));
    }

    /// <summary>AC-78 (iii) two SKUs (r25 F-7) — the rule itself: one row per SKU (X LotMissing, Y NotFromStaging), unique per skuId, no task for either.</summary>
    [Fact]
    public void AC78_iii_two_skus_give_two_rows_one_per_sku()
    {
        var staging = Guid.NewGuid();
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        var outbound = new OutboundShipment
        {
            StagingLocationId = staging,
            Lines =
            [
                new OutboundLine { LineSeq = 1, SkuId = x, ItemId = x, SkuLevel = SkuLevel.Lsku, Allocations = [new OutboundAllocation { AllocSeq = 1, LotId = "LX" }] },
                new OutboundLine { LineSeq = 2, SkuId = y, ItemId = y, SkuLevel = SkuLevel.Lsku, Allocations = [new OutboundAllocation { AllocSeq = 2, LotId = "LY" }] }
            ]
        };
        var plan = OutboundFlowRules.ShortDispatch(outbound, new Dictionary<int, decimal> { [1] = 10m, [2] = 10m },
            [new IssuedLine(x, null, staging, 8m), new IssuedLine(y, "LY", Guid.NewGuid(), 8m)]);
        Assert.Empty(plan.Putbacks);
        Assert.Equal([(x, "LotMissing"), (y, "NotFromStaging")], plan.Unresolved.Select(u => (u.SkuId, u.Reason)).ToList());
    }

    /// <summary>
    /// AC-31 (MOD-0177 invariance, XMC-001): the two new IShipmentReader members have DEFAULT bodies — a class that writes only ReadAsync (the MOD-0177
    /// FakeShipmentReader pattern) compiles and answers Unavailable through the interface. Sabotage (em): a default body removed → this file (and the MOD-0177
    /// test project) do not compile.
    /// </summary>
    [Fact]
    public async Task AC31_the_new_reader_members_have_default_bodies_that_answer_unavailable()
    {
        IShipmentReader reader = new OnlyReadAsyncReader();
        Assert.Equal(ShipmentReadOutcome.Unavailable, (await reader.QueryBySourceDocumentAsync("x")).Read);
        Assert.Equal(ShipmentReadOutcome.Unavailable, (await reader.ReadDetailAsync(Guid.NewGuid().ToString("D"))).Read);
    }

    internal sealed class OnlyReadAsyncReader : IShipmentReader
    {
        public Task<IReadOnlyDictionary<string, TraceShipmentInfo>> ReadAsync(IReadOnlyCollection<string> shipmentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, TraceShipmentInfo>>(new Dictionary<string, TraceShipmentInfo>());
    }
}

/// <summary>A host whose IShipmentReader writes only ReadAsync — 0178 maps the default bodies to 503 (AC-31 "MOD-0177 değişmezliği").</summary>
public sealed class DefaultShipmentReaderApiFactory : WarehouseApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddScoped<IShipmentReader, DispatchSyncTests.OnlyReadAsyncReader>());
    }
}

public sealed class DispatchDefaultReaderTests : IClassFixture<DefaultShipmentReaderApiFactory>
{
    private readonly DefaultShipmentReaderApiFactory _factory;

    public DispatchDefaultReaderTests(DefaultShipmentReaderApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC31_a_reader_with_only_ReadAsync_makes_the_sync_503_without_a_write()
    {
        var (s, f, _, _) = await PickTaskTests.FS2Async(_factory);
        var id = await f.ReadyAsync(s.Line(10));
        var version = (await f.ViewAsync(id)).GetProperty("version").GetInt32();
        Assert.Equal("WMS_DISPATCH_SOURCE_UNAVAILABLE", Code(await Expect(f.SyncAsync(id), 503)));
        Assert.Equal(version, (await f.ViewAsync(id)).GetProperty("version").GetInt32());
    }
}
