using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Persistence.Warehouse;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-4 AC-26 (pack §8.17 steps 2–5, §8.10) — the allocation on the REAL host: FEFO order limited by the FU03 scope free quantity and
/// by the AVAILABLE stock on pickable nodes (Staging and Receiving subtrees excluded), all or nothing, the persisted pick plan in location-code
/// order with open tasks and the request's own earlier distribution subtracted. Fixture F-S2: L1 (2027-01-31) 30 EA on A-02, L2 (2027-03-31)
/// 50 EA on A-01, Staging S-01.
/// </summary>
public sealed class OutboundAllocationTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundAllocationTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<(OutboundScenario S, string L1, string L2)> FS2Async()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var l1 = s.Lot("2027-01-31");
        var l2 = s.Lot("2027-03-31");
        await s.ReceiveAsync(30, s.A02, l1);
        await s.ReceiveAsync(50, s.A01, l2);
        return (s, l1, l2);
    }

    /// <summary>Sabotage (ce): a reservation with <c>expiresAt</c> → the "no expiresAt" assertion is red.</summary>
    [Fact]
    public async Task AC26_a_60_EA_outbound_is_Allocated_with_FEFO_allocations_FU03_reservations_and_two_pick_tasks()
    {
        var (s, l1, l2) = await FS2Async();

        var outbound = await s.CreatedAsync(s.Line(60, orderLineId: "SO-1/1"));
        var id = OutboundScenario.Id(outbound);

        Assert.Equal("Allocated", outbound.GetProperty("status").GetString());
        Assert.Matches(new Regex("^GMG-OUT-\\d{6}$"), outbound.GetProperty("outboundNumber").GetString()!);
        Assert.Equal(s.S01.ToString("D"), outbound.GetProperty("stagingLocationId").GetString());
        Assert.Equal([(l1, 30m, 1), (l2, 30m, 2)], AllocationSummary(outbound));

        var allocations = Allocations(outbound);
        for (var i = 0; i < allocations.Count; i++)
        {
            var reservationId = allocations[i].GetProperty("reservationId").GetString()!;
            var view = await s.ReservationViewAsync(reservationId);
            Assert.NotNull(view);
            Assert.Equal("WAREHOUSE_OUTBOUND", view!.ReservationType);
            Assert.Equal("MOD-0178", view.SourceModule);
            Assert.Equal(id, view.SourceDocumentId);
            Assert.Equal("1", view.SourceLineId);
            Assert.Null(view.ExpiresAt);                                   // Q-07: no expiresAt
            Assert.Null(view.Priority);
            Assert.Equal(i == 0 ? l1 : l2, view.LotId);
            Assert.Equal($"wms:out:{id}:{i + 1}", s.Reservations(id).Single(r => r["_id"].AsString == reservationId)["IdempotencyKey"].AsString);
        }

        var tasks = s.PickTasks(id).OrderBy(t => t["AllocSeq"].AsInt32).ToList();
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t =>
        {
            Assert.Matches(new Regex("^GMG-PCK-\\d{6}$"), t["Number"].AsString);
            Assert.Equal("Open", t["Status"].AsString);
            Assert.Equal("Outbound", t["Origin"].AsString);
            Assert.Equal(s.S01.ToString("D"), t["ToLocationId"].ToString());
        });
        Assert.Equal(s.A02.ToString("D"), tasks[0]["FromLocationId"].ToString());   // L1 lies on A-02
        Assert.Equal(s.A01.ToString("D"), tasks[1]["FromLocationId"].ToString());   // L2 on A-01
        Assert.Equal(["Created", "Allocated"], s.OutboundEvents(id).Select(e => e["Kind"].AsString));
        Assert.Equal((1, 0), s.Central("CreateOutboundShipmentCommand"));
        Assert.All(tasks, t => Assert.Single(s.P.W.Stored(WarehouseCollections.TaskEvents), e => e["TaskId"].ToString() == t["_id"].ToString() && e["Kind"].AsString == "Created"));
    }

    [Fact]
    public async Task AC26_an_uncoverable_line_is_422_with_nothing_written_and_no_partial_outbound()
    {
        var (s, _, _) = await FS2Async();

        var refused = await Expect(s.CreateAsync(s.Body(s.Line(100))), 422);
        Assert.Equal("WMS_ALLOCATION_FAILED", Code(refused));
        var line = Assert.Single(Details(refused).GetProperty("lines").EnumerateArray());
        Assert.Equal(1, line.GetProperty("lineSeq").GetInt32());
        Assert.Equal(100m, PutawayScenario.D(line, "requested"));
        Assert.Equal(80m, PutawayScenario.D(line, "allocatable"));

        // two lines, the first coverable: still all or nothing
        var both = await Expect(s.CreateAsync(s.Body(s.Line(10, orderLineId: "A"), s.Line(500, orderLineId: "B"))), 422);
        Assert.Equal(2, Assert.Single(Details(both).GetProperty("lines").EnumerateArray()).GetProperty("lineSeq").GetInt32());

        Assert.Equal(0, (await Expect(s.SearchAsync(), 200)).GetProperty("total").GetInt64());
        Assert.Empty(s.Reservations());
        Assert.Empty(s.PickTasks());
        Assert.Empty(s.StoredOutbounds());
        Assert.Empty(s.CentralRows("CreateOutboundShipmentCommand"));
    }

    /// <summary>Sabotage (cc): the plan without the FU03 scope free limit → FU03 refuses the batch → 422 (L1 10 + L2 50 expected).</summary>
    [Fact]
    public async Task AC26_another_outbounds_reservation_limits_the_lot_by_the_free_quantity()
    {
        var (s, l1, l2) = await FS2Async();
        await s.ReserveOtherAsync(20, l1);

        var outbound = await s.CreatedAsync(s.Line(60));

        Assert.Equal([(l1, 10m, 1), (l2, 50m, 2)], AllocationSummary(outbound));
    }

    /// <summary>Sabotage (dg): the rank counts a lot whose free quantity is 0 → FefoRank 1 = L1 (red).</summary>
    [Fact]
    public async Task AC26_a_wholly_reserved_lot_does_not_count_in_the_order()
    {
        var (s, l1, l2) = await FS2Async();
        await s.ReserveOtherAsync(30, l1);

        var outbound = await s.CreatedAsync(s.Line(30));

        Assert.Equal([(l2, 30m, 1)], AllocationSummary(outbound));
    }

    /// <summary>Sabotage (cd): the Staging stock counted in the allocatable quantity → L1 shows 70 (red).</summary>
    [Fact]
    public async Task AC26_stock_on_Staging_is_not_allocatable()
    {
        var (s, l1, _) = await FS2Async();
        await s.ReceiveAsync(40, s.S01, l1);

        var preview = await Expect(s.PreviewAsync(s.Body(s.Line(10))), 200);

        var lots = preview.GetProperty("lines")[0].GetProperty("lots").EnumerateArray().ToList();
        Assert.Equal(30m, PutawayScenario.D(lots.Single(l => l.GetProperty("lotId").GetString() == l1), "allocatable"));
        var refused = await Expect(s.CreateAsync(s.Body(s.Line(81))), 422);
        Assert.Equal(80m, PutawayScenario.D(Details(refused).GetProperty("lines")[0], "allocatable"));
    }

    [Fact]
    public async Task AC26_a_race_between_plan_and_batch_is_refused_by_FU03_with_no_reservation_and_no_visible_outbound()
    {
        var (s, l1, _) = await FS2Async();
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            await s.ReserveOtherAsync(30, l1);   // another exit takes L1 between the plan and the batch
            return await real();
        };

        var refused = await Expect(s.CreateAsync(s.Body(s.Line(60))), 422);

        Assert.Equal("WMS_ALLOCATION_FAILED", Code(refused));
        var line = Assert.Single(Details(refused).GetProperty("lines").EnumerateArray());
        Assert.Equal(1, line.GetProperty("lineSeq").GetInt32());
        Assert.False(string.IsNullOrEmpty(line.GetProperty("code").GetString()));   // FU03's own line code, passed on
        Assert.Empty(s.Reservations(DocId(s.StoredOutbounds().Single())));
        Assert.Equal("AllocationFailed", s.StoredOutbounds().Single()["Status"].AsString);
        Assert.Equal("BatchRefused", s.StoredOutbounds().Single()["AllocationFailure"]["Kind"].AsString);
        Assert.Equal(0, (await Expect(s.SearchAsync(), 200)).GetProperty("total").GetInt64());
        Assert.Empty(s.PickTasks());
        Assert.Equal((0, 1), s.Central("CreateOutboundShipmentCommand"));   // transaction A committed → the row is Abandoned
        Assert.Equal(["Created", "AllocationFailed"], s.OutboundEvents().Select(e => e["Kind"].AsString));
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
    }

    private static string DocId(MongoDB.Bson.BsonDocument document) => document["_id"].ToString()!;

    [Fact]
    public async Task AC26_the_preview_answers_the_same_lots_without_any_write()
    {
        var (s, l1, l2) = await FS2Async();
        var reservationsBefore = s.Reservations().Count;

        var preview = await Expect(s.PreviewAsync(s.Body(s.Line(60))), 200);

        var line = preview.GetProperty("lines")[0];
        Assert.Equal(60m, PutawayScenario.D(line, "allocated"));
        Assert.Equal([(l1, 30m, 1), (l2, 30m, 2)], line.GetProperty("allocations").EnumerateArray()
            .Select(a => (a.GetProperty("lotId").GetString(), PutawayScenario.D(a, "quantity"), a.GetProperty("fefoRank").GetInt32())).ToList());
        Assert.Equal(reservationsBefore, s.Reservations().Count);
        Assert.Empty(s.StoredOutbounds());
        Assert.Empty(s.OutboundEvents());
        Assert.Empty(s.PickTasks());
    }

    /// <summary>Sabotage (dj): the plan ignores the open pick tasks → Z's task opens on A-01 (red).</summary>
    [Fact]
    public async Task AC26_two_outbounds_on_one_lot_in_two_bins_take_the_next_bin()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var l3 = s.Lot("2027-02-28");
        await s.ReceiveAsync(10, s.A01, l3);
        await s.ReceiveAsync(10, s.A02, l3);

        var y = await s.CreatedAsync(s.Line(10));
        var z = await s.CreatedAsync(s.Line(10));

        Assert.Equal([(s.A01, 10m)], Tasks(y));
        Assert.Equal([(s.A02, 10m)], Tasks(z));
    }

    /// <summary>Sabotage (dt): Receiving not excluded from the pickable nodes → a 15 EA outbound opens a task on R-01 (red).</summary>
    [Fact]
    public async Task AC26_receiving_stock_with_an_open_putaway_task_is_never_picked()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var l5 = s.Lot("2027-02-28");
        await s.ReceiveAsync(10, s.R01, l5);
        await s.ReceiveAsync(10, s.A01, l5);
        var putaway = s.P.TaskBody(10, lotId: l5);
        await Expect(s.P.CreateTaskAsync(putaway), 201);

        var refused = await Expect(s.CreateAsync(s.Body(s.Line(15))), 422);
        Assert.Equal(10m, PutawayScenario.D(Details(refused).GetProperty("lines")[0], "allocatable"));

        var outbound = await s.CreatedAsync(s.Line(10));
        Assert.Equal([(s.A01, 10m)], Tasks(outbound));
        Assert.DoesNotContain(s.PickTasks(), t => t["FromLocationId"].ToString() == s.R01.ToString("D"));
    }

    /// <summary>Sabotage (ec): the lotless allocatable excludes only Staging (Receiving counted) → allocatable 20, BinCapacityShort (red).</summary>
    [Theory]
    [InlineData(SkuLevel.Lsku)]
    [InlineData(SkuLevel.Item)]
    public async Task AC26_a_lotless_SKU_uses_the_same_pickable_definition_with_one_warehouse_allocation(SkuLevel level)
    {
        var s = await new OutboundScenario(_factory, lotControlled: false, level: level).ReadyAsync();
        await s.ReceiveAsync(10, s.R01, null);
        await s.ReceiveAsync(10, s.A01, null);

        var refused = await Expect(s.CreateAsync(s.Body(s.Line(15))), 422);
        var line = Details(refused).GetProperty("lines")[0];
        Assert.Equal(10m, PutawayScenario.D(line, "allocatable"));
        Assert.NotEqual("BinCapacityShort", line.GetProperty("code").GetString());

        var outbound = await s.CreatedAsync(s.Line(10));
        var allocation = Assert.Single(Allocations(outbound));
        Assert.False(allocation.TryGetProperty("lotId", out _));
        Assert.Equal([(s.A01, 10m)], Tasks(outbound));
        var reservation = Assert.Single(s.Reservations(OutboundScenario.Id(outbound)));
        Assert.Equal(s.Warehouse.ToString("D"), reservation["WarehouseId"].AsString);
        Assert.False(reservation.Contains("LotId") && reservation["LotId"].IsString);
    }

    /// <summary>Sabotage (dr): the plan forgets the request's earlier distribution → A-01 gets 12 (red).</summary>
    [Fact]
    public async Task AC26_two_lines_of_one_request_on_the_same_bin_never_overplan_it()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var l4 = s.Lot("2027-02-28");
        await s.ReceiveAsync(10, s.A01, l4);
        await s.ReceiveAsync(10, s.A02, l4);

        var outbound = await s.CreatedAsync(s.Line(6, orderLineId: "SO/1"), s.Line(6, orderLineId: "SO/2"));

        var bins = outbound.GetProperty("lines").EnumerateArray()
            .Select(l => l.GetProperty("allocations").EnumerateArray().SelectMany(a => a.GetProperty("plannedBins").EnumerateArray())
                .Select(b => (Guid.Parse(b.GetProperty("locationId").GetString()!), PutawayScenario.D(b, "quantity"))).ToList())
            .ToList();
        Assert.Equal([(s.A01, 6m)], bins[0]);
        Assert.Equal([(s.A01, 4m), (s.A02, 2m)], bins[1]);
        Assert.Equal(10m, s.PickTasks().Where(t => t["FromLocationId"].ToString() == s.A01.ToString("D")).Sum(t => t["PlannedQuantity"].ToDecimal()));
    }

    /// <summary>
    /// r22 round 2 F-3 b — an open task's remaining is planned − Posted: a Pending confirmation still counts as remaining. Sabotage (dr, second
    /// half): remaining = planned − Posted − Pending → Z takes 6 from A-01 (red; 0 expected).
    /// </summary>
    [Fact]
    public async Task AC26_a_pending_confirmation_keeps_its_quantity_in_the_open_task_remaining()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var lot = s.Lot("2027-02-28");
        await s.ReceiveAsync(10, s.A01, lot);
        await s.ReceiveAsync(10, s.A02, lot);
        var y = await s.CreatedAsync(s.Line(10));
        var yTask = Assert.Single(s.PickTasks(OutboundScenario.Id(y)));

        // Y's task got a 6 EA confirmation that is still Pending (its movement's outcome unknown — the bin still holds the 10)
        var tasks = _factory.Database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);
        var taskId = Guid.Parse(yTask["_id"].ToString()!);
        var stored = await tasks.Find(t => t.Id == taskId).SingleAsync();
        stored.Confirmations.Add(new TaskConfirmation { Seq = 1, Quantity = 6, ToLocationId = s.S01, Status = ConfirmationStatus.Pending, PostingKey = $"wms:pck:{taskId}:1" });
        stored.Status = WarehouseTaskStatus.InProgress;
        stored.Version++;
        await tasks.ReplaceOneAsync(t => t.Id == taskId, stored);

        var z = await s.CreatedAsync(s.Line(10));

        Assert.Equal([(s.A02, 10m)], Tasks(z));
    }
}
