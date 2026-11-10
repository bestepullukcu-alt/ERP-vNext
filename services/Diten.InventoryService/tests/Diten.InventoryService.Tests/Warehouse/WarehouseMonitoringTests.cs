using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// One S3 world (DL-7) on top of <see cref="OutboundScenario"/> + <see cref="OutboundFlow"/>: the REAL S3 API (<c>GET /exceptions</c>, <c>/activity</c>,
/// <c>/integration/status</c>) with a monitor token, plus fixture documents written straight into the 0178 collections (an outbound / task in the state
/// the AC names — the same documents the S2 commands write; the stock itself always through the REAL ledger API).
/// </summary>
public sealed class MonitoringWorld
{
    public const string ExceptionsRead = "warehouse.exceptions.read";
    public const string ActivityRead = "warehouse.activity.read";
    public const string IntegrationRead = "warehouse.integration-monitor.read";

    public MonitoringWorld(OutboundScenario s, OutboundFlow f)
    {
        S = s;
        F = f;
        Monitor = s.TokenFor(ExceptionsRead, ActivityRead, IntegrationRead);
    }

    public OutboundScenario S { get; }

    public OutboundFlow F { get; }

    public string Monitor { get; }

    public DateTimeOffset Now => S.Factory.Clock.GetUtcNow();

    public IMongoCollection<OutboundShipment> Outbounds => S.Factory.Database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments);

    public IMongoCollection<WarehouseTask> Tasks => S.Factory.Database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);

    public static async Task<MonitoringWorld> CreateAsync(WarehouseApiFactory factory)
    {
        var (s, f, _, _) = await PickTaskTests.FS2Async(factory);
        return new MonitoringWorld(s, f);
    }

    // ── fixture documents ──

    public OutboundShipment Outbound(OutboundStatus status, Guid? staging = null, Guid? tenant = null, Guid? company = null, params OutboundLine[] lines)
    {
        var o = new OutboundShipment
        {
            Id = Guid.NewGuid(), TenantId = tenant ?? S.Tenant, LegalEntityId = company ?? S.LegalEntity, CreatedAt = Now, Version = 1,
            OutboundNumber = "GMG-OUT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), CompanyCodePrefix = "GMG", WarehouseId = S.Warehouse,
            StagingLocationId = staging ?? S.S01, Status = status, ShipTo = new OutboundAddress { Name = "Ada Hastanesi", Country = "TR", Line1 = "Atatürk Blv. 1" },
            Lines = [.. lines], CreatedBy = S.User, CorrelationId = Guid.NewGuid()
        };
        return o;
    }

    public OutboundLine Line(int seq, params OutboundAllocation[] allocations) => Line(seq, S.Sku, allocations);

    public OutboundLine Line(int seq, Guid sku, params OutboundAllocation[] allocations)
        => new()
        {
            LineSeq = seq, OrderLineId = "SO-" + seq, ItemId = S.Item, SkuId = sku, SkuLevel = SkuLevel.Lsku, RequestedQuantity = allocations.Sum(a => a.Quantity),
            BaseUomId = "EA", Allocations = [.. allocations]
        };

    public static OutboundAllocation Alloc(int seq, string? lot, decimal quantity, ReservationReleaseState state = ReservationReleaseState.None)
        => new()
        {
            AllocSeq = seq, LotId = lot, LotNumber = lot is null ? null : "LN-" + seq, Quantity = quantity, ReservationId = Guid.NewGuid(),
            ReservationKey = $"wms:out:{Guid.NewGuid():D}:{seq}", ReservationReleaseState = state
        };

    public OutboundShipment Put(OutboundShipment o)
    {
        Outbounds.InsertOne(o);
        return o;
    }

    /// <summary>A Completed pick task of <paramref name="o"/>'s allocation with <paramref name="picked"/> Posted (the derived PickedQuantity).</summary>
    public WarehouseTask Picked(OutboundShipment o, int allocSeq, decimal picked)
    {
        var allocation = o.AllAllocations().Single(a => a.AllocSeq == allocSeq);
        var line = o.Lines.Single(l => l.Allocations.Contains(allocation));
        var task = Task(WarehouseTaskKind.Pick, WarehouseTaskOrigin.Outbound, S.A01, line.SkuId, allocation.LotId, picked, WarehouseTaskStatus.Completed);
        task.OutboundId = o.Id;
        task.OutboundNumber = o.OutboundNumber;
        task.LineSeq = line.LineSeq;
        task.AllocSeq = allocSeq;
        task.ToLocationId = o.StagingLocationId;
        task.ConfirmedQuantity = picked;
        return PutTask(task);
    }

    public WarehouseTask Task(WarehouseTaskKind kind, WarehouseTaskOrigin origin, Guid from, Guid sku, string? lot, decimal planned,
        WarehouseTaskStatus status = WarehouseTaskStatus.Open)
        => new()
        {
            Id = Guid.NewGuid(), TenantId = S.Tenant, LegalEntityId = S.LegalEntity, CreatedAt = Now, Version = 1,
            Number = (kind == WarehouseTaskKind.Pick ? "GMG-PCK-" : "GMG-PUT-") + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), CompanyCodePrefix = "GMG",
            Kind = kind, Origin = origin, Status = status, WarehouseId = S.Warehouse, ItemId = S.Item, SkuId = sku, SkuLevel = SkuLevel.Lsku, LotId = lot,
            FromLocationId = from, PlannedQuantity = planned, BaseUomId = "EA", CreatedBy = S.User, CorrelationId = Guid.NewGuid()
        };

    public static TaskConfirmation Confirmation(int seq, ConfirmationStatus status, DateTimeOffset at, Guid to, ConfirmationResolution? resolution = null,
        string? code = null)
        => new()
        {
            Seq = seq, Quantity = 1m, ToLocationId = to, Status = status, PostingKey = $"wms:put:{Guid.NewGuid():D}:{seq}", TransactionDate = at, PostingDate = at,
            ConfirmedAtUtc = at, ConfirmedBy = Guid.NewGuid(), Resolution = resolution, ErrorCode = code, RefusedAtUtc = status == ConfirmationStatus.Refused ? at : null,
            Attempts = 1, LastAttemptAtUtc = at
        };

    public WarehouseTask PutTask(WarehouseTask task)
    {
        Tasks.InsertOne(task);
        return task;
    }

    public void SetStatus(OutboundShipment o, OutboundStatus status)
        => Outbounds.UpdateOne(x => x.Id == o.Id, Builders<OutboundShipment>.Update.Set(x => x.Status, status));

    // ── S3 API ──

    public async Task<JsonElement> ExceptionsAsync(string query = "", string? token = null)
        => await Expect(S.P.W.GetAsync("/api/warehouse/exceptions?pageSize=200" + query, token ?? Monitor), 200);

    public async Task<List<JsonElement>> ItemsAsync(string query = "", string? token = null)
        => (await ExceptionsAsync(query, token)).GetProperty("items").EnumerateArray().ToList();

    public static string Window(DateTimeOffset from, DateTimeOffset to)
        => $"from={Uri.EscapeDataString(from.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}"
           + $"&to={Uri.EscapeDataString(to.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}";

    public Task<HttpResponseMessage> ActivityAsync(string query, string? token = null) => S.P.W.GetAsync("/api/warehouse/activity?" + query, token ?? Monitor);

    public Task<HttpResponseMessage> IntegrationAsync(string? token = null) => S.P.W.GetAsync("/api/warehouse/integration/status", token ?? Monitor);

    public static string Id(JsonElement e) => e.GetProperty("exceptionId").GetString()!;

    /// <summary>The S3 schema rule (AC-36): no member named <c>shipTo</c> at any depth and none of the fixture's address values (a <c>shipToEntered</c> /
    /// <c>shipToChanged</c> MARK of an outbound timeline row is not the ship-to — pack §21).</summary>
    public static void NoShipTo(string body)
    {
        Assert.DoesNotContain("Ada Hastanesi", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Atatürk Blv", body, StringComparison.Ordinal);
        Walk(JsonDocument.Parse(body).RootElement);

        static void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject())
                {
                    Assert.False(string.Equals(p.Name, "shipTo", StringComparison.OrdinalIgnoreCase), "an S3 answer carries a shipTo member");
                    Walk(p.Value);
                }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in e.EnumerateArray()) Walk(x);
            }
        }
    }

    public static string Kind(JsonElement e) => e.GetProperty("kind").GetString()!;

    public static JsonElement Detail(JsonElement e, string name) => e.GetProperty("details").GetProperty(name);

    /// <summary>The <c>stagingDifference[]</c> of a record as (lotId, onHand, bound, difference).</summary>
    public static List<(string? Lot, decimal OnHand, decimal Bound, decimal Difference)> Differences(JsonElement e)
        => Detail(e, "stagingDifference").EnumerateArray().Select(d => (d.TryGetProperty("lotId", out var l) ? l.GetString() : null, PutawayScenario.D(d, "onHand"),
            PutawayScenario.D(d, "bound"), PutawayScenario.D(d, "difference"))).OrderBy(d => d.Item1, StringComparer.Ordinal).ToList();
}

/// <summary>
/// MOD-0178 DL-7 — the S3 monitoring reads (pack §8.23; AC-36, AC-71 (v) / (vii), AC-73 / AC-77 / AC-78 (iv) S3 parts): the exception box DERIVED at read time
/// (nine kinds, one record per row unit, <c>status Open</c>, the exit conditions), the Activity Trace (mandatory ≤ 31-day range, three sources, newest first),
/// the Integration Monitor's values, the company scope and the keys. No S3 answer carries a ship-to.
/// </summary>
public sealed class WarehouseMonitoringTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public WarehouseMonitoringTests(WarehouseApiFactory factory) => _factory = factory;

    /// <summary>
    /// AC-36: one record of each of the nine kinds (and the near-misses that must NOT be listed: a resolved Refused, a fresh Pending, a 1-hour ReadyToShip,
    /// a KeyReused outbound with every allocation Released, a BatchRefused outbound, a ShortDispatchUnresolved row whose Staging difference is 0, another
    /// company's and another tenant's records) → exactly nine, the right kind, <c>status Open</c>, no repeated <c>exceptionId</c>, no ship-to; the
    /// KeyReused outbound's Pending allocations are NOT a ReservationReleasePending as well. Sabotages: (da) the company predicate of the exception reads
    /// dropped → the other company's record is listed (red); (db) the DispatchNotSeenHours threshold dropped → the 1-hour ReadyToShip is listed (red);
    /// (dz) the "not every allocation Released" condition dropped → the all-Released KeyReused outbound is listed (red).
    /// </summary>
    [Fact]
    public async Task AC36_the_box_derives_each_of_the_nine_kinds_once_and_nothing_else()
    {
        var w = await MonitoringWorld.CreateAsync(_factory);
        var s = w.S;
        var now = w.Now;
        var recalled = s.Lot("2027-06-30");
        w.F.Recall(recalled);

        // posting kinds — a putaway task (unresolved Refused + resolved Refused) and a pick task (long Pending + fresh Pending + IntegrityError)
        var putaway = w.Task(WarehouseTaskKind.Putaway, WarehouseTaskOrigin.Manual, s.R01, s.Sku, null, 10m, WarehouseTaskStatus.InProgress);
        putaway.Confirmations =
        [
            MonitoringWorld.Confirmation(1, ConfirmationStatus.Refused, now.AddMinutes(-5), s.A01, code: "LOT_UNDER_RECALL"),
            MonitoringWorld.Confirmation(2, ConfirmationStatus.Refused, now.AddMinutes(-4), s.A01, ConfirmationResolution.ReturnedToSource)
        ];
        w.PutTask(putaway);
        var pick = w.Task(WarehouseTaskKind.Pick, WarehouseTaskOrigin.Outbound, s.A01, s.Sku, null, 10m, WarehouseTaskStatus.InProgress);
        pick.Confirmations =
        [
            MonitoringWorld.Confirmation(1, ConfirmationStatus.Pending, now.AddMinutes(-31), s.A02),
            MonitoringWorld.Confirmation(2, ConfirmationStatus.Pending, now.AddMinutes(-2), s.A02),
            MonitoringWorld.Confirmation(3, ConfirmationStatus.IntegrityError, now.AddMinutes(-3), s.A02, code: "IDEMPOTENCY_KEY_REUSED")
        ];
        w.PutTask(pick);

        // outbound kinds (the ReadyToShip ones on their own Staging node: on S-01 with the SKU they would defer S-01's difference — §8.23 F-1 (b))
        var dock = s.P.W.Node(LocationType.Bin, s.StagingZone).Id;
        var notSeen = w.Outbound(OutboundStatus.ReadyToShip, dock, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        notSeen.ReadyAtUtc = now.AddHours(-25);
        w.Put(notSeen);
        var fresh = w.Outbound(OutboundStatus.ReadyToShip, dock, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        fresh.ReadyAtUtc = now.AddHours(-1);
        w.Put(fresh);
        var cancelled = w.Put(w.Outbound(OutboundStatus.Cancelled, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m, ReservationReleaseState.Pending))));
        var stuck = w.Outbound(OutboundStatus.Allocating, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        stuck.CreatedAt = now.AddSeconds(-241);
        w.Put(stuck);
        var young = w.Outbound(OutboundStatus.Allocating, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        young.CreatedAt = now.AddSeconds(-200);
        w.Put(young);
        var underRecall = w.Put(w.Outbound(OutboundStatus.Picked, lines: w.Line(1, MonitoringWorld.Alloc(1, recalled, 3m))));
        var orphan = w.Outbound(OutboundStatus.AllocationFailed,
            lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m, ReservationReleaseState.Pending), MonitoringWorld.Alloc(2, null, 3m, ReservationReleaseState.Released)));
        orphan.AllocationFailure = new OutboundAllocationFailure { Kind = AllocationFailureKind.KeyReused, AtUtc = now.AddMinutes(-9) };
        w.Put(orphan);
        var released = w.Outbound(OutboundStatus.AllocationFailed, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m, ReservationReleaseState.Released)));
        released.AllocationFailure = new OutboundAllocationFailure { Kind = AllocationFailureKind.KeyReused, AtUtc = now.AddMinutes(-9) };
        w.Put(released);
        var batch = w.Outbound(OutboundStatus.AllocationFailed, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        batch.AllocationFailure = new OutboundAllocationFailure { Kind = AllocationFailureKind.BatchRefused, AtUtc = now.AddMinutes(-9) };
        w.Put(batch);

        // ShortDispatchUnresolved — S-01 holds 5 of L-S nobody owns (difference +5); another Staging node S-02 with nothing (difference 0 → not listed)
        var lotS = s.Lot("2027-08-31");
        await s.ReceiveAsync(5, s.S01, lotS);
        var shortOpen = w.Outbound(OutboundStatus.Shipped, lines: w.Line(1, MonitoringWorld.Alloc(1, lotS, 10m, ReservationReleaseState.Released)));
        shortOpen.ShortDispatchUnresolved = [new OutboundShortDispatchUnresolved { SkuId = s.Sku, Expected = 10m, Issued = 5m, Reason = "LotMissing" }];
        w.Put(shortOpen);
        var s02 = s.P.W.Node(LocationType.Bin, s.StagingZone);
        var shortClosed = w.Outbound(OutboundStatus.Shipped, s02.Id, lines: w.Line(1, MonitoringWorld.Alloc(1, lotS, 10m, ReservationReleaseState.Released)));
        shortClosed.ShortDispatchUnresolved = [new OutboundShortDispatchUnresolved { SkuId = s.Sku, Expected = 10m, Issued = 5m, Reason = "LotMissing" }];
        w.Put(shortClosed);

        // another company of the SAME tenant and another tenant: an old ReadyToShip each — never in this company's box
        var otherCompany = w.Outbound(OutboundStatus.ReadyToShip, company: Guid.NewGuid(), lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        otherCompany.ReadyAtUtc = now.AddHours(-30);
        w.Put(otherCompany);
        var otherTenant = w.Outbound(OutboundStatus.ReadyToShip, tenant: Guid.NewGuid(), lines: w.Line(1, MonitoringWorld.Alloc(1, null, 3m)));
        otherTenant.ReadyAtUtc = now.AddHours(-30);
        w.Put(otherTenant);

        using var raw = await s.P.W.GetAsync("/api/warehouse/exceptions?pageSize=200", w.Monitor);
        var text = await raw.Content.ReadAsStringAsync();
        Assert.Equal(200, (int)raw.StatusCode);
        MonitoringWorld.NoShipTo(text);
        var page = JsonDocument.Parse(text).RootElement;
        var items = page.GetProperty("items").EnumerateArray().ToList();

        var expected = new Dictionary<string, string>
        {
            [$"PostingRefused:{putaway.Id:D}:1"] = "PostingRefused",
            [$"PostingPendingLong:{pick.Id:D}:1"] = "PostingPendingLong",
            [$"PostingIntegrityError:{pick.Id:D}:3"] = "PostingIntegrityError",
            [$"DispatchNotSeen:{notSeen.Id:D}"] = "DispatchNotSeen",
            [$"ReservationReleasePending:{cancelled.Id:D}:1"] = "ReservationReleasePending",
            [$"ShortDispatchUnresolved:{shortOpen.Id:D}:{s.Sku:D}"] = "ShortDispatchUnresolved",
            [$"AllocationOrphanSuspected:{orphan.Id:D}"] = "AllocationOrphanSuspected",
            [$"ReservedLotUnderRecall:{underRecall.Id:D}:1"] = "ReservedLotUnderRecall",
            [$"AllocationStuck:{stuck.Id:D}"] = "AllocationStuck"
        };
        Assert.Equal(expected.OrderBy(e => e.Key, StringComparer.Ordinal), items.Select(i => KeyValuePair.Create(MonitoringWorld.Id(i), MonitoringWorld.Kind(i)))
            .OrderBy(e => e.Key, StringComparer.Ordinal));
        Assert.Equal(9, page.GetProperty("total").GetInt64());
        Assert.Equal(items.Count, items.Select(MonitoringWorld.Id).Distinct().Count());
        Assert.All(items, i => Assert.Equal("Open", i.GetProperty("status").GetString()));
        Assert.All(items, i => Assert.Equal(s.Warehouse.ToString("D"), i.GetProperty("warehouseId").GetString()));

        // the details the screen reads
        var refused = items.Single(i => MonitoringWorld.Kind(i) == "PostingRefused");
        Assert.Equal(("LOT_UNDER_RECALL", putaway.Id.ToString("D"), putaway.Number, "Putaway"), (refused.GetProperty("code").GetString(),
            refused.GetProperty("taskId").GetString(), MonitoringWorld.Detail(refused, "taskNumber").GetString(), MonitoringWorld.Detail(refused, "taskKind").GetString()));
        var orphanRow = items.Single(i => MonitoringWorld.Kind(i) == "AllocationOrphanSuspected");
        Assert.Equal(1, MonitoringWorld.Detail(orphanRow, "pending").GetInt32());
        Assert.Equal(2, MonitoringWorld.Detail(orphanRow, "reservationKeys").GetArrayLength());
        var shortRow = items.Single(i => MonitoringWorld.Kind(i) == "ShortDispatchUnresolved");
        Assert.Equal([(lotS, 5m, 0m, 5m)], MonitoringWorld.Differences(shortRow));
        Assert.Equal(JsonValueKind.Null, MonitoringWorld.Detail(shortRow, "differenceDeferredBy").ValueKind);
        Assert.Equal(shortOpen.OutboundNumber, MonitoringWorld.Detail(shortRow, "outboundNumber").GetString());

        // the kind and warehouse filters; a wrong kind is 400
        Assert.Equal([$"DispatchNotSeen:{notSeen.Id:D}"], (await w.ItemsAsync("&kind=DispatchNotSeen")).Select(MonitoringWorld.Id));
        Assert.Empty(await w.ItemsAsync("&warehouseId=" + Guid.NewGuid().ToString("D")));
        Assert.Equal(9, (await w.ItemsAsync("&warehouseId=" + s.Warehouse.ToString("D"))).Count);
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.P.W.GetAsync("/api/warehouse/exceptions?kind=OutboundDrift", w.Monitor), 400));
        Assert.Equal(2, (await Expect(s.P.W.GetAsync("/api/warehouse/exceptions?page=2&pageSize=7", w.Monitor), 200)).GetProperty("items").GetArrayLength());

        // the other company of the same tenant sees its own record only; the other tenant sees nothing of this tenant
        var otherCompanyToken = TestTokens.Mint(s.Tenant, otherCompany.LegalEntityId!.Value, s.User, [MonitoringWorld.ExceptionsRead]);
        Assert.Equal([$"DispatchNotSeen:{otherCompany.Id:D}"], (await w.ItemsAsync(string.Empty, otherCompanyToken)).Select(MonitoringWorld.Id));
        var stranger = await MonitoringWorld.CreateAsync(_factory);
        Assert.Empty(await stranger.ItemsAsync());
    }

    /// <summary>
    /// AC-36 <c>exceptionId</c> uniqueness (r25, son bakış 03 F-7): a Shipped outbound with two unresolved SKU rows (X, Y — both differences ≠ 0) gives two
    /// records <c>…:{outboundId}:X</c> / <c>…:Y</c>; a Cancelled outbound with two Pending allocations gives <c>…:1</c> / <c>…:2</c>; a Picked outbound whose two
    /// allocation lots are under recall gives two records; no id repeats. Sabotage (en): the skuId left out of the ShortDispatchUnresolved key → a repeat (red).
    /// </summary>
    [Fact]
    public async Task AC36_every_exception_id_is_unique_per_row_unit()
    {
        var w = await MonitoringWorld.CreateAsync(_factory);
        var s = w.S;
        var x = s.Sku;
        var y = Guid.NewGuid();
        // the bound side opens the differences: an open ShortDispatch put-back of 4 from S-01 per SKU and no stock there → −4 each
        w.PutTask(w.Task(WarehouseTaskKind.Putaway, WarehouseTaskOrigin.ShortDispatch, s.S01, x, null, 4m));
        w.PutTask(w.Task(WarehouseTaskKind.Putaway, WarehouseTaskOrigin.ShortDispatch, s.S01, y, null, 4m));
        var shipped = w.Outbound(OutboundStatus.Shipped,
            lines: [w.Line(1, x, MonitoringWorld.Alloc(1, null, 10m, ReservationReleaseState.Released)), w.Line(2, y, MonitoringWorld.Alloc(2, null, 10m, ReservationReleaseState.Released))]);
        shipped.ShortDispatchUnresolved =
        [
            new OutboundShortDispatchUnresolved { SkuId = x, Expected = 10m, Issued = 6m, Reason = "LotMissing" },
            new OutboundShortDispatchUnresolved { SkuId = y, Expected = 10m, Issued = 6m, Reason = "NotFromStaging" }
        ];
        w.Put(shipped);
        var cancelled = w.Put(w.Outbound(OutboundStatus.Cancelled,
            lines: w.Line(1, MonitoringWorld.Alloc(1, null, 2m, ReservationReleaseState.Pending), MonitoringWorld.Alloc(2, null, 2m, ReservationReleaseState.Pending))));
        var l1 = s.Lot("2027-04-30");
        var l2 = s.Lot("2027-05-31");
        w.F.Recall(l1);
        w.F.Recall(l2);
        var picked = w.Put(w.Outbound(OutboundStatus.Picked, lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 2m), MonitoringWorld.Alloc(2, l2, 2m))));

        var items = await w.ItemsAsync();

        string[] expected =
        [
            $"ReservationReleasePending:{cancelled.Id:D}:1", $"ReservationReleasePending:{cancelled.Id:D}:2",
            $"ReservedLotUnderRecall:{picked.Id:D}:1", $"ReservedLotUnderRecall:{picked.Id:D}:2",
            $"ShortDispatchUnresolved:{shipped.Id:D}:{x:D}", $"ShortDispatchUnresolved:{shipped.Id:D}:{y:D}"
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), items.Select(MonitoringWorld.Id).Order(StringComparer.Ordinal));
        Assert.Equal(items.Count, items.Select(MonitoringWorld.Id).ToHashSet(StringComparer.Ordinal).Count);
        Assert.All(items.Where(i => MonitoringWorld.Kind(i) == "ShortDispatchUnresolved"), i => Assert.Equal([(null, 0m, 4m, -4m)], MonitoringWorld.Differences(i)));
    }

    /// <summary>
    /// AC-78 (iv) (a) through the REAL S2 flow: L1 30 picked to S-01, ReadyToShip; MVP-6 issues 25 from S-01 and its line has NO lot → Shipped with one
    /// LotMissing row → the box lists it with <c>stagingDifference [{ L1, +5 }]</c>; while a Pending pick confirmation of the SKU goes INTO S-01 the
    /// difference is not computed (<c>null</c>, <c>differenceDeferredBy PendingConfirmation</c>); after the 0178-external S-01 → A-01 transfer of 5 the
    /// record drops; the stored row never changes. Sabotage (dy): the difference condition removed (a stored row always listed) → the "drops" step red.
    /// </summary>
    [Fact]
    public async Task AC78_iv_a_the_short_dispatch_record_stays_while_the_staging_difference_is_open_and_drops_when_it_closes()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var w = new MonitoringWorld(s, f);
        var id = await f.ReadyAsync(s.Line(30));
        var reservationId = OutboundFlow.Allocs(await f.ViewAsync(id)).Single().GetProperty("reservationId").GetString()!;
        await f.IssueAsync(25, s.S01, l1, reservationId);
        f.Dispatched((s.Sku, null, s.S01, 25m));
        Assert.Equal("Shipped", (await Expect(f.SyncAsync(id), 200)).GetProperty("outbound").GetProperty("status").GetString());
        var storedRow = s.StoredOutbound(id)["ShortDispatchUnresolved"].ToString();

        var record = Assert.Single(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
        Assert.Equal($"ShortDispatchUnresolved:{id}:{s.Sku:D}", MonitoringWorld.Id(record));
        Assert.Equal(("LotMissing", 30m, 25m), (MonitoringWorld.Detail(record, "reason").GetString(), PutawayScenario.D(record.GetProperty("details"), "expected"),
            PutawayScenario.D(record.GetProperty("details"), "issued")));
        Assert.Equal([(l1, 5m, 0m, 5m)], MonitoringWorld.Differences(record));

        // a Pending pick confirmation of the SKU into S-01 → not computed, still listed
        var pending = w.Task(WarehouseTaskKind.Pick, WarehouseTaskOrigin.Outbound, s.A02, s.Sku, l1, 5m, WarehouseTaskStatus.InProgress);
        pending.Confirmations = [MonitoringWorld.Confirmation(1, ConfirmationStatus.Pending, w.Now, s.S01)];
        w.PutTask(pending);
        var deferred = Assert.Single(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
        Assert.Equal(JsonValueKind.Null, MonitoringWorld.Detail(deferred, "stagingDifference").ValueKind);
        Assert.Equal("PendingConfirmation", MonitoringWorld.Detail(deferred, "differenceDeferredBy").GetString());
        w.Tasks.DeleteOne(t => t.Id == pending.Id);

        await f.TransferAsync(5, s.S01, s.A01, l1);                                      // the INVENTORY correction, outside 0178

        Assert.Empty(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
        Assert.Equal(storedRow, s.StoredOutbound(id)["ShortDispatchUnresolved"].ToString());
    }

    /// <summary>
    /// AC-78 (iv) (b) the lot net does not hide (F-1 (a)): O1 Shipped with an OverIssued X row (expected 20, issued 20 — SKU net 0); S-01 holds L2 5 nobody
    /// owns and Packed O2's L1 5 is gone → <c>[{ L1, onHand 0, bound 5, −5 }, { L2, onHand 5, bound 0, +5 }]</c> listed; S-01 → A-01 L2 5 → still listed with
    /// <c>[{ L1, −5 }]</c>; A-02 → S-01 L1 5 → drops. Sabotage (ej): the SKU-level net → net 0, the record is not listed (red).
    /// </summary>
    [Fact]
    public async Task AC78_iv_b_a_lot_surplus_is_not_hidden_by_another_lots_shortage()
    {
        var (s, f, l1, l2) = await PickTaskTests.FS2Async(_factory);
        var w = new MonitoringWorld(s, f);
        var o1 = w.Outbound(OutboundStatus.Shipped,
            lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 10m, ReservationReleaseState.Released), MonitoringWorld.Alloc(2, l2, 10m, ReservationReleaseState.Released)));
        o1.ShortDispatchUnresolved = [new OutboundShortDispatchUnresolved { SkuId = s.Sku, Expected = 20m, Issued = 20m, Reason = "OverIssued" }];
        w.Put(o1);
        var o2 = w.Put(w.Outbound(OutboundStatus.Packed, lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 5m))));
        w.Picked(o2, 1, 5m);
        await f.TransferAsync(5, s.A01, s.S01, l2);                                      // S-01: L2 5, L1 0

        var record = Assert.Single(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
        Assert.Equal([(l1, 0m, 5m, -5m), (l2, 5m, 0m, 5m)], MonitoringWorld.Differences(record).OrderBy(d => d.Lot == l1 ? 0 : 1).ToList());

        await f.TransferAsync(5, s.S01, s.A01, l2);
        Assert.Equal([(l1, 0m, 5m, -5m)], MonitoringWorld.Differences(Assert.Single(await w.ItemsAsync("&kind=ShortDispatchUnresolved"))));

        await f.TransferAsync(5, s.A02, s.S01, l1);
        Assert.Empty(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
    }

    /// <summary>
    /// AC-78 (iv) (c) an unsynced ReadyToShip (F-1 (b)): the LotMissing state (S-01 L1 5 nobody owns) plus ReadyToShip O3 on S-01 with X L1 5 picked whose 5
    /// MVP-6 already issued (S-01 holds 5) — computed it would read 0 → listed with <c>null</c> / <c>UnsyncedReadyToShip</c>; O3 synced (Shipped) →
    /// <c>[{ L1, +5 }]</c>; S-01 → A-01 5 → drops. Sabotage (ek): the difference computed despite the ReadyToShip → 0, the record drops (red).
    /// </summary>
    [Fact]
    public async Task AC78_iv_c_an_unsynced_ready_to_ship_outbound_defers_the_difference()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var w = new MonitoringWorld(s, f);
        var o1 = w.Outbound(OutboundStatus.Shipped, lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 30m, ReservationReleaseState.Released)));
        o1.ShortDispatchUnresolved = [new OutboundShortDispatchUnresolved { SkuId = s.Sku, LotId = l1, Expected = 30m, Issued = 25m, Reason = "LotMissing" }];
        w.Put(o1);
        await f.TransferAsync(5, s.A02, s.S01, l1);                                      // S-01: L1 5 nobody owns
        var o3 = w.Outbound(OutboundStatus.ReadyToShip, lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 5m)));
        o3.ReadyAtUtc = w.Now;
        w.Put(o3);
        w.Picked(o3, 1, 5m);                                                              // its 5 already issued by MVP-6: not on S-01 any more

        var deferred = Assert.Single(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
        Assert.Equal(JsonValueKind.Null, MonitoringWorld.Detail(deferred, "stagingDifference").ValueKind);
        Assert.Equal("UnsyncedReadyToShip", MonitoringWorld.Detail(deferred, "differenceDeferredBy").GetString());

        w.SetStatus(o3, OutboundStatus.Shipped);                                          // O3 synced
        Assert.Equal([(l1, 5m, 0m, 5m)], MonitoringWorld.Differences(Assert.Single(await w.ItemsAsync("&kind=ShortDispatchUnresolved"))));

        await f.TransferAsync(5, s.S01, s.A01, l1);
        Assert.Empty(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
    }

    /// <summary>
    /// AC-78 (iv) (d) one node (F-2): S-10 a stockable StorageLocation with the Staging role, S-11 its bin (Staging inherited). O1 (Staging S-10) has a
    /// LotMissing row and S-10 holds nothing → not listed; Packed O4 (Staging S-11) holds L1 5 on S-11 → O1 is still NOT listed (S-11 is out of S-10's
    /// scope). Sabotage (el): the OnHand read from S-10's subtree (the bound on the node) → S-11's 5 counted → listed (red).
    /// </summary>
    [Fact]
    public async Task AC78_iv_d_the_difference_is_measured_on_exactly_the_staging_node()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var w = new MonitoringWorld(s, f);
        var s10 = LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.StorageLocation, "S-10-" + Guid.NewGuid().ToString("N")[..6], s.StagingZone, stockable: true);
        var s11 = LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.Bin, "S-11-" + Guid.NewGuid().ToString("N")[..6], s10);
        LocationSeed.Insert(_factory.Database, s10, s11);
        var o1 = w.Outbound(OutboundStatus.Shipped, s10.Id, lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 30m, ReservationReleaseState.Released)));
        o1.ShortDispatchUnresolved = [new OutboundShortDispatchUnresolved { SkuId = s.Sku, LotId = l1, Expected = 30m, Issued = 25m, Reason = "LotMissing" }];
        w.Put(o1);
        Assert.Empty(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));

        var o4 = w.Put(w.Outbound(OutboundStatus.Packed, s11.Id, lines: w.Line(1, MonitoringWorld.Alloc(1, l1, 5m))));
        w.Picked(o4, 1, 5m);
        await f.TransferAsync(5, s.A02, s11.Id, l1);                                     // O4's picked 5 on S-11

        Assert.Empty(await w.ItemsAsync("&kind=ShortDispatchUnresolved"));
        Assert.Equal(5m, await s.P.OnHandAsync(s11.Id));
    }

    /// <summary>
    /// AC-71 (v) + AC-77 scope (r22, tur 2 F-4) through the REAL allocation: the batch committed and the process stopped → <c>Allocating</c>; its lot goes under
    /// recall → no ReservedLotUnderRecall (internal state) and not yet AllocationStuck; past 2 × AllocationLeaseSeconds → AllocationStuck (and still no
    /// recall record); the maintenance completes it (<c>Allocated</c>) → AllocationStuck drops, ReservedLotUnderRecall appears; the outbound is cancelled
    /// (reservation released) → the record drops (AC-77 exit).
    /// </summary>
    [Fact]
    public async Task AC71_v_AC77_an_allocating_outbound_is_stuck_after_two_leases_and_its_recalled_lot_shows_once_allocated()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var w = new MonitoringWorld(s, f);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            var answer = await real();
            if (Interlocked.Increment(ref calls) > 1) return answer;
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(60));
            throw new InvalidOperationException("test: the process is gone");
        };
        Task<HttpResponseMessage> request;
        string id;
        try
        {
            request = s.CreateAsync(s.Body(s.Line(10)));
            await WaitForAsync(() => s.Reservations().Count == 1);
            id = s.StoredOutbounds().Single()["_id"].ToString()!;
            Assert.Equal("Allocating", s.StoredOutbound(id)["Status"].AsString);
            f.Recall(l1);

            Assert.Empty(await w.ItemsAsync());

            _factory.Clock.Advance(TimeSpan.FromSeconds(241));
            Assert.Equal([$"AllocationStuck:{id}"], (await w.ItemsAsync()).Select(MonitoringWorld.Id));

            Assert.Equal(1, (await s.MaintainAsync()).CommandsSent);
            Assert.Equal("Allocated", s.StoredOutbound(id)["Status"].AsString);
            Assert.Equal([$"ReservedLotUnderRecall:{id}:1"], (await w.ItemsAsync()).Select(MonitoringWorld.Id));
        }
        finally
        {
            stopped.TrySetResult();
        }

        await Expect(request, 503);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);

        await Expect(f.CancelAsync(id), 200);
        Assert.Equal("Released", OutboundFlow.Alloc(await f.ViewAsync(id), 1).GetProperty("reservationReleaseState").GetString());
        Assert.Empty(await w.ItemsAsync());
    }

    /// <summary>
    /// AC-71 (vii) S3 part through the REAL FU03 400 branch: AllocationFailed (KeyReused) with three Pending allocations → ONE AllocationOrphanSuspected
    /// (<c>pending 3</c>, three keys), NOT a ReservationReleasePending; the maintenance releases by key → every allocation Released → the record drops.
    /// Sabotage (dz): the "not every allocation Released" condition dropped → still listed after the release (red).
    /// </summary>
    [Fact]
    public async Task AC71_vii_the_orphan_record_drops_when_the_maintenance_released_every_allocation()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var w = new MonitoringWorld(s, new OutboundFlow(s));
        foreach (var (expiry, bin) in new[] { ("2027-01-31", s.A01), ("2027-02-28", s.A02), ("2027-03-31", s.A01) }) await s.ReceiveAsync(10, bin, s.Lot(expiry));
        _factory.AroundBatch[s.Tenant] = async (lines, real) =>
        {
            using var scope = _factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.LegalEntity, s.User);
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
            var port = scope.ServiceProvider.GetRequiredService<InventoryReservationPort>();
            var first = await port.CreateAsync(lines[0], s.LegalEntity, s.User);
            await port.CreateAsync(lines[1], s.LegalEntity, s.User);
            Assert.True((await port.ReleaseAsync(Guid.Parse(first.Data!.ReservationId), first.Data.Etag, s.User)).IsSuccessful);
            return await real();
        };
        await Expect(s.CreateAsync(s.Body(s.Line(30))), 422);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
        var id = s.StoredOutbounds().Single()["_id"].ToString()!;

        var record = Assert.Single(await w.ItemsAsync());
        Assert.Equal($"AllocationOrphanSuspected:{id}", MonitoringWorld.Id(record));
        Assert.Equal(3, MonitoringWorld.Detail(record, "pending").GetInt32());
        Assert.Equal(3, MonitoringWorld.Detail(record, "reservationKeys").GetArrayLength());

        await s.MaintainAsync();

        Assert.All(s.StoredOutbound(id)["Lines"].AsBsonArray.SelectMany(l => l["Allocations"].AsBsonArray), a => Assert.Equal("Released", a["ReservationReleaseState"].AsString));
        Assert.Empty(await w.ItemsAsync());
    }

    /// <summary>
    /// AC-36 Activity Trace through the REAL S2 flow (allocate → pick): the range is mandatory and ≤ 31 days (none / 32 days / to before from → 400); the answer
    /// holds task timeline rows, outbound timeline rows and the <c>sourceModule MOD-0178</c> movement, newest first; <c>kind</c> narrows to one source,
    /// another warehouse is empty, another tenant sees nothing; no ship-to.
    /// </summary>
    [Fact]
    public async Task AC36_the_activity_trace_merges_task_outbound_and_movement_rows_newest_first_in_a_mandatory_range()
    {
        var (s, f, _, _) = await PickTaskTests.FS2Async(_factory);
        var w = new MonitoringWorld(s, f);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(10)));
        await f.PickAllAsync(id);
        var now = w.Now;
        var window = MonitoringWorld.Window(now.AddHours(-1), now.AddHours(1));

        using var raw = await w.ActivityAsync(window + "&pageSize=200");
        var text = await raw.Content.ReadAsStringAsync();
        Assert.Equal(200, (int)raw.StatusCode);
        MonitoringWorld.NoShipTo(text);
        var page = JsonDocument.Parse(text).RootElement;
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("source").GetString() == "Outbound" && i.GetProperty("recordId").GetString() == id && i.GetProperty("kind").GetString() == "Created");
        Assert.Contains(items, i => i.GetProperty("source").GetString() == "PickTask" && i.GetProperty("kind").GetString() == "ConfirmationPosted");
        var movement = Assert.Single(items, i => i.GetProperty("source").GetString() == "Movement");
        Assert.Equal("LOCATION_TRANSFER", movement.GetProperty("kind").GetString());
        Assert.Equal("PICK_TASK", movement.GetProperty("data").GetProperty("sourceType").GetString());
        Assert.Equal(page.GetProperty("total").GetInt64(), items.Count);
        var times = items.Select(i => DateTimeOffset.Parse(i.GetProperty("atUtc").GetString()!, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(times.OrderByDescending(t => t), times);                              // newest first

        Assert.All((await Expect(w.ActivityAsync(window + "&kind=Movement"), 200)).GetProperty("items").EnumerateArray(),
            i => Assert.Equal("Movement", i.GetProperty("source").GetString()));
        Assert.Equal(0, (await Expect(w.ActivityAsync(window + "&warehouseId=" + Guid.NewGuid().ToString("D")), 200)).GetProperty("total").GetInt64());
        Assert.Equal("INVALID_REQUEST", await ErrorCode(w.ActivityAsync("pageSize=10"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(w.ActivityAsync(MonitoringWorld.Window(now.AddDays(-32), now)), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(w.ActivityAsync(MonitoringWorld.Window(now, now.AddDays(-1))), 400));
        Assert.Equal(200, (int)(await w.ActivityAsync(MonitoringWorld.Window(now.AddDays(-31), now))).StatusCode);

        var stranger = await MonitoringWorld.CreateAsync(_factory);
        Assert.Equal(0, (await Expect(stranger.ActivityAsync(window), 200)).GetProperty("total").GetInt64());
    }

    /// <summary>
    /// AC-36 Integration Monitor: the fixture's outbox rows (2 Pending — the oldest 90 min — and 1 Delivered), the ReadyToShip outbounds (2, the oldest
    /// ReadyAtUtc), the last Shipped write and the Pending confirmations (1 putaway + 2 pick) come back on the four cards; another company's rows do not
    /// count; no ship-to.
    /// </summary>
    [Fact]
    public async Task AC36_the_integration_monitor_returns_the_four_cards_values_of_the_company()
    {
        var w = await MonitoringWorld.CreateAsync(_factory);
        var s = w.S;
        var now = w.Now;
        var outbox = _factory.Database.GetCollection<WarehouseOutboxMessage>(WarehouseCollections.Outbox);
        WarehouseOutboxMessage Row(string status, DateTimeOffset at, Guid? company = null)
            => new() { Id = Guid.NewGuid(), TenantId = s.Tenant, LegalEntityId = company ?? s.LegalEntity, AggregateId = Guid.NewGuid(), EventId = Guid.NewGuid(), Status = status, OccurredAt = at };
        outbox.InsertMany([Row("Pending", now.AddMinutes(-90)), Row("Pending", now.AddMinutes(-10)), Row(WarehouseMonitoringReads.DeliveredStatus, now.AddMinutes(-200)),
            Row("Pending", now.AddDays(-3), Guid.NewGuid())]);
        var older = w.Outbound(OutboundStatus.ReadyToShip, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 1m)));
        older.ReadyAtUtc = now.AddHours(-3);
        w.Put(older);
        var newer = w.Outbound(OutboundStatus.ReadyToShip, lines: w.Line(1, MonitoringWorld.Alloc(1, null, 1m)));
        newer.ReadyAtUtc = now.AddHours(-1);
        w.Put(newer);
        var shippedAt = now.AddMinutes(-42);
        _factory.Database.GetCollection<OutboundShipmentEvent>(WarehouseCollections.OutboundEvents).InsertMany(
        [
            new OutboundShipmentEvent { Id = Guid.NewGuid(), TenantId = s.Tenant, LegalEntityId = s.LegalEntity, OutboundId = older.Id, Seq = 9, Kind = OutboundEventKind.Shipped, AtUtc = shippedAt.AddHours(-5) },
            new OutboundShipmentEvent { Id = Guid.NewGuid(), TenantId = s.Tenant, LegalEntityId = s.LegalEntity, OutboundId = newer.Id, Seq = 9, Kind = OutboundEventKind.Shipped, AtUtc = shippedAt }
        ]);
        var putaway = w.Task(WarehouseTaskKind.Putaway, WarehouseTaskOrigin.Manual, s.R01, s.Sku, null, 5m, WarehouseTaskStatus.InProgress);
        putaway.Confirmations = [MonitoringWorld.Confirmation(1, ConfirmationStatus.Pending, now, s.A01), MonitoringWorld.Confirmation(2, ConfirmationStatus.Posted, now, s.A01)];
        w.PutTask(putaway);
        var pick = w.Task(WarehouseTaskKind.Pick, WarehouseTaskOrigin.Outbound, s.A01, s.Sku, null, 5m, WarehouseTaskStatus.InProgress);
        pick.Confirmations = [MonitoringWorld.Confirmation(1, ConfirmationStatus.Pending, now, s.S01), MonitoringWorld.Confirmation(2, ConfirmationStatus.Pending, now, s.S01)];
        w.PutTask(pick);

        using var raw = await w.IntegrationAsync();
        var text = await raw.Content.ReadAsStringAsync();
        Assert.Equal(200, (int)raw.StatusCode);
        MonitoringWorld.NoShipTo(text);
        var status = JsonDocument.Parse(text).RootElement;

        var queue = status.GetProperty("eventQueue");
        Assert.Equal((2, 1), (queue.GetProperty("pending").GetInt64(), queue.GetProperty("delivered").GetInt64()));
        Assert.InRange(queue.GetProperty("oldestPendingAgeSeconds").GetInt64(), 90 * 60 - 5, 90 * 60 + 120);
        var dispatch = status.GetProperty("awaitingDispatch");
        Assert.Equal(2, dispatch.GetProperty("count").GetInt64());
        Assert.Equal(older.ReadyAtUtc!.Value.UtcDateTime, DateTimeOffset.Parse(dispatch.GetProperty("oldestReadyAtUtc").GetString()!, CultureInfo.InvariantCulture).UtcDateTime,
            TimeSpan.FromMilliseconds(5));
        Assert.Equal(shippedAt.UtcDateTime, DateTimeOffset.Parse(status.GetProperty("lastSync").GetProperty("lastShippedAtUtc").GetString()!, CultureInfo.InvariantCulture).UtcDateTime,
            TimeSpan.FromMilliseconds(5));
        var confirmations = status.GetProperty("pendingConfirmations");
        Assert.Equal((1, 2, 3), (confirmations.GetProperty("putaway").GetInt64(), confirmations.GetProperty("pick").GetInt64(), confirmations.GetProperty("total").GetInt64()));

        var stranger = await MonitoringWorld.CreateAsync(_factory);
        var empty = await Expect(stranger.IntegrationAsync(), 200);
        Assert.Equal(0, empty.GetProperty("eventQueue").GetProperty("pending").GetInt64());
        Assert.False(empty.GetProperty("lastSync").TryGetProperty("lastShippedAtUtc", out _));
    }

    /// <summary>
    /// AC-73 (S3 endpoints): no token 401; a token without the endpoint's own key (every OTHER warehouse key) 403; the key alone 200; an
    /// <c>X-Legal-Entity-Id</c> that is not the token's company 403 <c>CROSS_LE_FORBIDDEN</c>. Sabotage (1): <c>[HasPermission]</c> removed from one S3 action →
    /// the 403 row of that endpoint red (and <c>AuthorizationGuardTests</c>).
    /// </summary>
    [Fact]
    public async Task AC73_each_S3_endpoint_needs_its_own_key_and_the_selected_company()
    {
        var w = await MonitoringWorld.CreateAsync(_factory);
        var s = w.S;
        var window = MonitoringWorld.Window(w.Now.AddHours(-1), w.Now);
        var endpoints = new (string Path, string Key)[]
        {
            ("/api/warehouse/exceptions", MonitoringWorld.ExceptionsRead),
            ("/api/warehouse/activity?" + window, MonitoringWorld.ActivityRead),
            ("/api/warehouse/integration/status", MonitoringWorld.IntegrationRead)
        };
        string[] all = [MonitoringWorld.ExceptionsRead, MonitoringWorld.ActivityRead, MonitoringWorld.IntegrationRead, OutboundScenario.OutboundRead, OutboundFlow.PickRead,
            OutboundFlow.PackagesRead, PutawayScenario.TaskRead, Read];
        foreach (var (path, key) in endpoints)
        {
            Assert.Equal(401, (int)(await s.P.W.GetAsync(path, string.Empty)).StatusCode);
            Assert.Equal(403, (int)(await s.P.W.GetAsync(path, s.TokenFor(all.Where(k => k != key).ToArray()))).StatusCode);
            Assert.Equal(200, (int)(await s.P.W.GetAsync(path, s.TokenFor(key))).StatusCode);
            var crossed = await s.P.W.GetAsync(path, s.TokenFor(key), r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", Guid.NewGuid().ToString("D")));
            Assert.Equal(403, (int)crossed.StatusCode);
            Assert.Contains("CROSS_LE_FORBIDDEN", await crossed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("condition not reached");
            await Task.Delay(50);
        }
    }
}
