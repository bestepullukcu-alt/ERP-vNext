using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Trace;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Trace;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// An in-memory <see cref="IRecallTraceSource"/> — the seeded graphs of the WP-0177-R2 engine tests. It answers exactly as the Mongo source
/// does (the caller's company only; other companies as a count), and can shuffle every list it returns (the determinism proof: the
/// engine, not the read order, fixes the node order and the seal).
/// </summary>
public sealed class FakeTraceSource : IRecallTraceSource
{
    public static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public List<TraceLot> Lots { get; } = [];
    public List<TraceLink> Links { get; } = [];
    public List<TraceTransaction> Transactions { get; } = [];
    public List<TraceBalance> Balances { get; } = [];
    public List<TraceSerial> Serials { get; } = [];
    public Dictionary<string, Guid> TypedLinks { get; } = new(StringComparer.Ordinal);
    public Dictionary<Guid, bool?> LotControlled { get; } = [];
    public long OtherCompany { get; set; }
    public bool Shuffle { get; set; }
    public int LinkQueries { get; private set; }

    private readonly Random _random = new(177);

    private IReadOnlyList<T> Out<T>(IEnumerable<T> rows)
    {
        var list = rows.ToList();
        return Shuffle ? list.OrderBy(_ => _random.Next()).ToList() : list;
    }

    public Guid Sku { get; } = Guid.Parse("5a000000-0000-0000-0000-000000000001");
    public Guid Item { get; } = Guid.Parse("1e000000-0000-0000-0000-000000000001");

    public TraceLot Lot(string number, Guid? id = null, bool serialized = false, Guid? sku = null)
    {
        var lot = new TraceLot(id ?? Guid.NewGuid(), number, Item, sku ?? Sku, SkuLevel.Lsku, serialized);
        Lots.Add(lot);
        return lot;
    }

    public void Link(TraceLot parent, TraceLot child, string reference) => Links.Add(new TraceLink(parent.Id, child.Id, reference, "1", "EA"));

    public TraceTransaction Move(MovementType type, string? lotId, decimal quantity, string? document = null, string module = "MOD-TEST", int day = 0,
        Guid? sku = null, string uom = "EA", string? reverses = null, string[]? serials = null, string? id = null)
    {
        var n = Transactions.Count + 1;
        var t = new TraceTransaction(id ?? $"txn-{n:D4}", $"INV-2026-{n:D6}", type, lotId, Item, sku ?? Sku, SkuLevel.Lsku, quantity, uom, module, document,
            T0.AddDays(day), reverses, serials ?? []);
        Transactions.Add(t);
        return t;
    }

    public void Stock(TraceLot lot, decimal onHand, string warehouse = "wh-a", StockStatus status = StockStatus.Available, string uom = "EA", Guid? sku = null)
        => Balances.Add(new TraceBalance(lot.Id.ToString("D"), lot.ItemId, sku ?? lot.SkuId, warehouse, "loc-1", status, onHand, uom));

    public Task<IReadOnlyList<TraceLot>> LotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => Task.FromResult(Out(Lots.Where(l => lotIds.Contains(l.Id))));

    public Task<IReadOnlyList<TraceLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default)
    {
        LinkQueries++;
        return Task.FromResult(Out(Links.Where(l => childLotIds.Contains(l.ChildLotId))));
    }

    public Task<IReadOnlyList<TraceLink>> ChildLinksAsync(IReadOnlyCollection<Guid> parentLotIds, CancellationToken cancellationToken = default)
    {
        LinkQueries++;
        return Task.FromResult(Out(Links.Where(l => parentLotIds.Contains(l.ParentLotId))));
    }

    public Task<IReadOnlyDictionary<Guid, TraceTransaction>> FirstInboundAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, TraceTransaction>>(Transactions
            .Where(t => TraceRules.Inbound.Contains(t.Type) && Guid.TryParse(t.LotId, out var l) && lotIds.Contains(l))
            .GroupBy(t => Guid.Parse(t.LotId!)).ToDictionary(g => g.Key, g => g.OrderBy(t => t.PostingDate).ThenBy(t => t.Id, StringComparer.Ordinal).First()));

    public Task<IReadOnlyList<TraceTransaction>> TransactionsOfLotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
    {
        var ids = lotIds.Select(l => l.ToString("D")).ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(Out(Transactions.Where(t => (t.LotId is not null && ids.Contains(t.LotId))
                                                           || (t.Type == MovementType.Reversal && t.ReversesTransactionId is not null))));
    }

    public Task<IReadOnlyList<TraceBalance>> BalancesAsync(IReadOnlyCollection<Guid> itemIds, IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
    {
        var ids = lotIds.Select(l => l.ToString("D")).ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(Out(Balances.Where(b => ids.Contains(b.LotId) && b.OnHand > 0)));
    }

    public Task<IReadOnlyList<TraceTransaction>> TransactionsOfDocumentAsync(string sourceModule, string sourceDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(Out(Transactions.Where(t => t.SourceModule == sourceModule && t.SourceDocumentId == sourceDocumentId)));

    public Task<IReadOnlyList<TraceTransaction>> OutboundOfSkuAsync(Guid itemId, Guid skuId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => Task.FromResult(Out(Transactions.Where(t => t.ItemId == itemId && t.SkuId == skuId && t.PostingDate >= from && t.PostingDate <= to)));

    public Task<IReadOnlyDictionary<string, Guid>> TypedLotLinksAsync(IReadOnlyCollection<string> transactionIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, Guid>>(TypedLinks.Where(k => transactionIds.Contains(k.Key)).ToDictionary(k => k.Key, k => k.Value));

    public Task<long> OtherCompanyActivityAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default) => Task.FromResult(OtherCompany);

    public Task<IReadOnlyList<TraceSerial>> SerialsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => Task.FromResult(Out(Serials.Where(s => lotIds.Contains(s.LotId))));

    public Task<bool?> LotControlledAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
        => Task.FromResult(LotControlled.TryGetValue(skuId, out var c) ? c : true);

    public Task<IReadOnlyList<TraceSalesIssue>> SalesIssuesOfLotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TraceSalesIssue>>([]); // the engine never asks (R7a: getCaseCustomers only)
}

/// <summary>The MVP-6 shipment read double of the engine tests: one fixed outcome for every shipment, and the ids it was asked.</summary>
public sealed class FakeShipmentReader(ShipmentReadOutcome outcome = ShipmentReadOutcome.Ok) : IShipmentReader
{
    public ShipmentReadOutcome Outcome { get; set; } = outcome;

    public List<string> Asked { get; } = [];

    public Task<IReadOnlyDictionary<string, TraceShipmentInfo>> ReadAsync(IReadOnlyCollection<string> shipmentIds, CancellationToken cancellationToken = default)
    {
        Asked.AddRange(shipmentIds);
        return Task.FromResult<IReadOnlyDictionary<string, TraceShipmentInfo>>(shipmentIds.ToDictionary(id => id, id => Outcome == ShipmentReadOutcome.Ok
            ? new TraceShipmentInfo(ShipmentReadOutcome.Ok, "Delivered", FakeTraceSource.T0.AddDays(9), "CUST-" + id[..4])
            : new TraceShipmentInfo(Outcome, null, null, null)));
    }
}

/// <summary>
/// WP-0177-R2 — the trace engine (pack §8.2) on seeded graphs: levels, kinds, cycle, diamond, limits (never silent), "Takip dışı" (never a
/// gap), the gap rules, serials, per-unit totals, the CT-R8 company rule, the shipment degrade, the reversal netting and the golden seal.
/// </summary>
public sealed class RecallTraceEngineTests
{
    private const string Shp1 = "0b5e0000-0000-0000-0000-000000000001";
    private const string Shp2 = "0b5e0000-0000-0000-0000-000000000002";

    private static Task<TraceResult?> Run(FakeTraceSource source, TraceLot root, RecallTraceDirection direction = RecallTraceDirection.Both,
        int maxDepth = 20, int maxNodes = 5000, IShipmentReader? reader = null, DateTimeOffset? asOf = null)
        => new RecallTraceEngine(source, reader ?? new FakeShipmentReader())
            .RunAsync(new TraceRequest(root.Id, direction, asOf ?? FakeTraceSource.T0.AddDays(30), maxDepth, maxNodes), FakeTraceSource.T0.AddDays(30), default);

    private static async Task<TraceResult> RunOk(FakeTraceSource source, TraceLot root, RecallTraceDirection direction = RecallTraceDirection.Both,
        int maxDepth = 20, int maxNodes = 5000, IShipmentReader? reader = null, DateTimeOffset? asOf = null)
        => Assert.IsType<TraceResult>(await Run(source, root, direction, maxDepth, maxNodes, reader, asOf));

    private static TraceNode Node(TraceResult r, string key) => Assert.Single(r.Nodes, n => n.NodeKey == key);

    private static string L(TraceLot lot) => TraceRules.LotKey(lot.Id.ToString("D"));

    private static string Id(TraceLot lot) => lot.Id.ToString("D");

    /// <summary>
    /// The golden graph: two raw-material lots → a bulk finished good (MO-1) → a packed lot (MO-2), the packed lot in stock and shipped on two
    /// MVP-6 shipments, one scrap — fixed ids and dates, so its seal is a constant.
    /// </summary>
    private static (FakeTraceSource Source, TraceLot Rm1, TraceLot Rm2, TraceLot Bulk, TraceLot Pack) Golden()
    {
        var s = new FakeTraceSource();
        var rm1 = s.Lot("RM-2026-001", Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var rm2 = s.Lot("RM-2026-002", Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var bulk = s.Lot("FG-2026-010", Guid.Parse("10000000-0000-0000-0000-000000000010"));
        var pack = s.Lot("PK-2026-100", Guid.Parse("10000000-0000-0000-0000-000000000100"));
        s.Move(MovementType.GoodsReceiptPo, Id(rm1), 500m, "PO-1", "MOD-0172", 0);
        s.Move(MovementType.GoodsReceiptPo, Id(rm2), 300m, "PO-2", "MOD-0172", 0);
        s.Move(MovementType.GoodsIssueProd, Id(rm1), 200m, "MO-1", "MOD-0194", 1);
        s.Move(MovementType.GoodsIssueProd, Id(rm2), 100m, "MO-1", "MOD-0194", 1);
        s.Move(MovementType.GoodsReceiptProd, Id(bulk), 50m, "MO-1", "MOD-0194", 2);
        s.Link(rm1, bulk, "MO-1");
        s.Link(rm2, bulk, "MO-1");
        s.Move(MovementType.GoodsIssueProd, Id(bulk), 50m, "MO-2", "MOD-0194", 3);
        s.Move(MovementType.GoodsReceiptProd, Id(pack), 1000m, "MO-2", "MOD-0194", 4);
        s.Link(bulk, pack, "MO-2");
        s.Move(MovementType.GoodsIssueSales, Id(pack), 300m, Shp1, "MOD-0183", 6);
        s.Move(MovementType.GoodsIssueSales, Id(pack), 200m, Shp2, "MOD-0183", 7);
        s.Move(MovementType.Scrap, Id(pack), 5m, null, "MOD-0173", 8);
        s.Stock(pack, 450m, "wh-a", StockStatus.Available);
        s.Stock(pack, 45m, "wh-b", StockStatus.Quarantine);
        return (s, rm1, rm2, bulk, pack);
    }

    // ── levels / kinds / batching ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Multi_level_trace_walks_both_sides_with_kinds_levels_and_one_link_query_per_level()
    {
        var (s, rm1, rm2, bulk, pack) = Golden();

        var r = await RunOk(s, bulk);

        Assert.Equal(TraceNodeKind.FinishedGoodLot, Node(r, L(bulk)).Kind);
        Assert.Equal((TraceSide.Root, 0), (Node(r, L(bulk)).Side, Node(r, L(bulk)).Level));
        Assert.Equal((TraceNodeKind.RawMaterialLot, TraceSide.Backward, 1), (Node(r, L(rm1)).Kind, Node(r, L(rm1)).Side, Node(r, L(rm1)).Level));
        Assert.Equal((TraceNodeKind.RawMaterialLot, TraceSide.Backward, 1), (Node(r, L(rm2)).Kind, Node(r, L(rm2)).Side, Node(r, L(rm2)).Level));
        Assert.Equal((TraceNodeKind.FinishedGoodLot, TraceSide.Forward, 1), (Node(r, L(pack)).Kind, Node(r, L(pack)).Side, Node(r, L(pack)).Level));
        Assert.Equal(L(bulk), Node(r, L(pack)).ParentKey);

        // the raw materials' source is their PO receipt (an Input row, not a gap)
        Assert.Equal(TraceNodeKind.Input, Node(r, TraceRules.SourceInputKey(Id(rm1))).Kind);

        // forward: the packed lot's stock (2 warehouses), 2 shipments and the scrap
        Assert.Equal(2, r.Nodes.Count(n => n.Kind == TraceNodeKind.Stock));
        Assert.Equal(2, r.Nodes.Count(n => n.Kind == TraceNodeKind.Shipment));
        Assert.Single(r.Nodes, n => n.Kind == TraceNodeKind.Disposal);
        Assert.Empty(r.Gaps);
        Assert.False(r.Truncated);
        Assert.Equal(2, r.Summary.FinishedGoodLots);

        // one batched link query per level: backward 1 level + the empty level, forward 1 level + the empty level
        Assert.Equal(4, s.LinkQueries);
        Assert.Equal(3, r.Edges.Count);
        Assert.All(r.Edges, e => Assert.False(e.Cycle));
    }

    [Fact]
    public async Task Direction_backward_reads_no_forward_stock_or_shipments_of_children()
    {
        var (s, _, _, bulk, pack) = Golden();

        var r = await RunOk(s, bulk, RecallTraceDirection.Backward);

        Assert.DoesNotContain(r.Nodes, n => n.NodeKey == L(pack));
        Assert.DoesNotContain(r.Nodes, n => n.Kind == TraceNodeKind.Shipment);
        Assert.Equal("Backward", r.Direction);
    }

    [Fact]
    public async Task Unknown_root_is_null()
        => Assert.Null(await Run(new FakeTraceSource(), new TraceLot(Guid.NewGuid(), "X", Guid.NewGuid(), Guid.NewGuid(), SkuLevel.Lsku, false)));

    // ── cycle / diamond ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cycle_is_one_visit_per_lot_and_an_edge_marked_cycle_never_a_truncated_walk()
    {
        var s = new FakeTraceSource();
        var a = s.Lot("CY-A");
        var b = s.Lot("CY-B");
        var c = s.Lot("CY-C");
        s.Move(MovementType.GoodsReceiptPo, Id(a), 10m);
        s.Link(a, b, "MO-1");
        s.Link(b, c, "MO-2");
        s.Link(c, a, "MO-3"); // bad data: a loop back to the root

        var r = await RunOk(s, a);

        Assert.False(r.Truncated);
        Assert.DoesNotContain(r.Gaps, g => g.GapCode == TraceGapCode.LIMIT_REACHED);
        Assert.Equal(3, r.Nodes.Count(n => TraceRules.IsLotKind(n.Kind)));
        Assert.Contains(r.Edges, e => e.Cycle);
        Assert.True(s.LinkQueries < 10, $"the walk re-read the loop: {s.LinkQueries} link queries");
    }

    [Fact]
    public async Task Diamond_reaches_the_shared_ancestor_once_and_marks_no_cycle()
    {
        var s = new FakeTraceSource();
        var top = s.Lot("DI-TOP");
        var left = s.Lot("DI-LEFT");
        var right = s.Lot("DI-RIGHT");
        var bottom = s.Lot("DI-BOTTOM");
        s.Link(top, left, "MO-L");
        s.Link(top, right, "MO-R");
        s.Link(left, bottom, "MO-B");
        s.Link(right, bottom, "MO-B");

        var r = await RunOk(s, bottom, RecallTraceDirection.Backward);

        Assert.Equal(2, Node(r, L(top)).Level);
        Assert.Single(r.Nodes, n => n.NodeKey == L(top));
        Assert.Equal(4, r.Edges.Count);
        Assert.All(r.Edges, e => Assert.False(e.Cycle));
        Assert.False(r.Truncated);
    }

    // ── limits (never silent) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Depth_limit_truncates_with_a_LIMIT_REACHED_gap()
    {
        var s = new FakeTraceSource();
        var chain = Enumerable.Range(0, 6).Select(i => s.Lot($"CH-{i}")).ToList();
        for (var i = 0; i < 5; i++) s.Link(chain[i + 1], chain[i], $"MO-{i}");

        var r = await RunOk(s, chain[0], RecallTraceDirection.Backward, maxDepth: 2);

        Assert.True(r.Truncated);
        var gap = Assert.Single(r.Gaps, g => g.GapCode == TraceGapCode.LIMIT_REACHED);
        Assert.Contains("depth 2", gap.Text);
        Assert.Equal(3, r.Nodes.Count(n => TraceRules.IsLotKind(n.Kind)));
        Assert.Equal((2, 5000), (r.MaxDepth, r.MaxNodes));
    }

    [Fact]
    public async Task Node_limit_truncates_with_a_LIMIT_REACHED_gap()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("NL-ROOT");
        for (var i = 0; i < 10; i++) s.Link(s.Lot($"NL-{i:D2}"), root, $"MO-{i}");

        var r = await RunOk(s, root, RecallTraceDirection.Backward, maxNodes: 4);

        Assert.True(r.Truncated);
        Assert.Single(r.Gaps, g => g.GapCode == TraceGapCode.LIMIT_REACHED);
        Assert.Equal(4, r.Nodes.Count(n => TraceRules.IsLotKind(n.Kind)));
    }

    [Fact]
    public async Task Within_the_limits_there_is_no_LIMIT_REACHED_gap()
    {
        var (s, _, _, bulk, _) = Golden();
        var r = await RunOk(s, bulk, maxDepth: 1);
        Assert.False(r.Truncated);
        Assert.DoesNotContain(r.Gaps, g => g.GapCode == TraceGapCode.LIMIT_REACHED);
    }

    // ── inputs: "Takip dışı" is not a gap; missing links are ──────────────────────────────────────────────

    [Fact]
    public async Task Non_lot_input_is_Takip_disi_and_never_a_gap_while_unlinked_lot_controlled_inputs_are_gaps()
    {
        var s = new FakeTraceSource();
        var linked = s.Lot("IN-LINKED");
        var unlinked = s.Lot("IN-UNLINKED");
        var fg = s.Lot("IN-FG");
        var packaging = Guid.Parse("5a000000-0000-0000-0000-0000000000aa"); // not lot-controlled
        var solvent = Guid.Parse("5a000000-0000-0000-0000-0000000000bb");   // lot-controlled, issued without a lot
        s.LotControlled[packaging] = false;
        s.LotControlled[solvent] = true;
        s.Move(MovementType.GoodsIssueProd, Id(linked), 10m, "MO-7", "MOD-0194");
        s.Move(MovementType.GoodsIssueProd, Id(unlinked), 4m, "MO-7", "MOD-0194");
        s.Move(MovementType.GoodsIssueProd, null, 250m, "MO-7", "MOD-0194", sku: packaging);
        s.Move(MovementType.GoodsIssueProd, null, 3m, "MO-7", "MOD-0194", sku: solvent, uom: "L");
        s.Move(MovementType.GoodsReceiptProd, Id(fg), 100m, "MO-7", "MOD-0194", 1);
        s.Link(linked, fg, "MO-7");

        var r = await RunOk(s, fg, RecallTraceDirection.Backward);

        var untracked = Node(r, TraceRules.UntrackedInputKey(Id(fg), packaging));
        Assert.True(untracked.Untracked);
        Assert.Equal(TraceNodeKind.Input, untracked.Kind);
        Assert.Equal(250m, untracked.Quantity);
        Assert.DoesNotContain(r.Gaps, g => g.SkuId == packaging);
        Assert.Equal(2, r.Gaps.Count(g => g.GapCode == TraceGapCode.MISSING_GENEALOGY_LINK));
        Assert.Contains(r.Gaps, g => g.NodeKey.EndsWith(Id(unlinked), StringComparison.Ordinal));
        Assert.Contains(r.Gaps, g => g.SkuId == solvent);
        Assert.Equal(2, r.Summary.GapCount);
    }

    [Fact]
    public async Task Production_issue_without_an_output_link_is_CONSUMED_WITHOUT_OUTPUT_LINK()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("PW-ROOT");
        var output = s.Lot("PW-OUT");
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m);
        s.Move(MovementType.GoodsIssueProd, Id(root), 30m, "MO-LINKED", "MOD-0194", 1);
        s.Move(MovementType.GoodsIssueProd, Id(root), 20m, "MO-NOLINK", "MOD-0194", 2);
        s.Link(root, output, "MO-LINKED");

        var r = await RunOk(s, root, RecallTraceDirection.Forward);

        var gap = Assert.Single(r.Gaps);
        Assert.Equal(TraceGapCode.CONSUMED_WITHOUT_OUTPUT_LINK, gap.GapCode);
        Assert.Equal("MO-NOLINK", gap.Movement!.SourceDocumentId);
        Assert.Equal(20m, gap.Quantity);
    }

    // ── outbound gap rules ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lotless_and_unresolved_typed_lot_outbound_are_gaps_and_a_migrated_typed_lot_joins_the_trace()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("TY-ROOT");
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m, day: 0);
        s.Move(MovementType.GoodsIssueSales, null, 7m, Shp1, "MOD-0183", 2);                                       // lotless
        s.Move(MovementType.GoodsIssueSales, "LOT-TXT-9", 3m, Shp2, "MOD-0183", 3);                                // typed, not migrated
        var migrated = s.Move(MovementType.GoodsIssueSales, "TY-ROOT-TXT", 5m, "0b5e0000-0000-0000-0000-000000000003", "MOD-0183", 4);
        s.TypedLinks[migrated.Id] = root.Id;
        s.Move(MovementType.GoodsIssueSales, null, 9m, null, "MOD-TEST", -5);                                      // before the receipt: outside the window

        var r = await RunOk(s, root, RecallTraceDirection.Forward);

        Assert.Single(r.Gaps, g => g.GapCode == TraceGapCode.LOTLESS_OUTBOUND);
        Assert.Single(r.Gaps, g => g.GapCode == TraceGapCode.UNRESOLVED_TYPED_LOT);
        var shipment = Node(r, TraceRules.ShipmentKey(Id(root), "0b5e0000-0000-0000-0000-000000000003"));
        Assert.Equal(5m, shipment.Quantity);
    }

    [Fact]
    public async Task A_sku_that_is_not_lot_controlled_has_no_lotless_gap()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("NC-ROOT");
        s.LotControlled[s.Sku] = false;
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m);
        s.Move(MovementType.GoodsIssueSales, null, 7m, Shp1, "MOD-0183", 2);

        Assert.Empty((await RunOk(s, root, RecallTraceDirection.Forward)).Gaps);
    }

    [Fact]
    public async Task Sales_issue_without_a_document_is_a_shipment_with_a_NO_CUSTOMER_REFERENCE_gap()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("NR-ROOT");
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m);
        var sale = s.Move(MovementType.GoodsIssueSales, Id(root), 12m, null, "MOD-TEST", 1);

        var r = await RunOk(s, root, RecallTraceDirection.Forward);

        var node = Node(r, TraceRules.ShipmentKey(Id(root), "TXN-" + sale.Id));
        Assert.Null(node.CustomerRef);
        var gap = Assert.Single(r.Gaps);
        Assert.Equal((TraceGapCode.NO_CUSTOMER_REFERENCE, node.NodeKey), (gap.GapCode!.Value, gap.ParentKey));
    }

    [Fact]
    public async Task Warehouse_transfer_is_internal_and_reversed_movements_do_not_count()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("RV-ROOT");
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m);
        s.Move(MovementType.WarehouseTransfer, Id(root), 40m, null, "MOD-0173", 1);
        var sale = s.Move(MovementType.GoodsIssueSales, Id(root), 10m, Shp1, "MOD-0183", 2);
        s.Move(MovementType.Reversal, Id(root), 10m, null, "MOD-0173", 3, reverses: sale.Id);

        var r = await RunOk(s, root, RecallTraceDirection.Forward);

        Assert.DoesNotContain(r.Nodes, n => n.Kind is TraceNodeKind.Shipment or TraceNodeKind.Disposal);
        Assert.Empty(r.Gaps);
    }

    [Fact]
    public async Task Movements_after_asOf_are_not_in_the_trace()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("AO-ROOT");
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m);
        s.Move(MovementType.GoodsIssueSales, Id(root), 10m, Shp1, "MOD-0183", 2);
        s.Move(MovementType.GoodsIssueSales, Id(root), 10m, Shp2, "MOD-0183", 20);

        var r = await RunOk(s, root, RecallTraceDirection.Forward, asOf: FakeTraceSource.T0.AddDays(10));

        Assert.Single(r.Nodes, n => n.Kind == TraceNodeKind.Shipment);
    }

    // ── serials ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Serial_tracked_lot_shows_its_serials_on_stock_and_shipment_rows()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("SR-ROOT", serialized: true);
        s.Move(MovementType.GoodsReceiptPo, Id(root), 3m);
        s.Move(MovementType.GoodsIssueSales, Id(root), 1m, Shp1, "MOD-0183", 1, serials: ["SN-003"]);
        s.Stock(root, 2m, "wh-a");
        s.Serials.Add(new TraceSerial(root.Id, "SN-002", "wh-a", "loc-1", true));
        s.Serials.Add(new TraceSerial(root.Id, "SN-001", "wh-a", "loc-1", true));
        s.Serials.Add(new TraceSerial(root.Id, "SN-003", null, null, false));

        var r = await RunOk(s, root, RecallTraceDirection.Forward);

        var stock = Assert.Single(r.Nodes, n => n.Kind == TraceNodeKind.Stock);
        Assert.Equal(new[] { "SN-001", "SN-002" }, stock.Serials.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "SN-003" }, Assert.Single(r.Nodes, n => n.Kind == TraceNodeKind.Shipment).Serials);
    }

    // ── totals: per SKU + unit only ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Totals_are_per_sku_and_base_unit_never_summed_across_units()
    {
        var s = new FakeTraceSource();
        var root = s.Lot("UN-ROOT");
        var other = Guid.Parse("5a000000-0000-0000-0000-0000000000cc");
        var child = s.Lot("UN-CHILD", sku: other);
        s.Move(MovementType.GoodsReceiptPo, Id(root), 100m);
        s.Link(root, child, "MO-1");
        s.Stock(root, 10m, "wh-a", uom: "EA");
        s.Stock(root, 2.5m, "wh-b", uom: "KG");
        s.Stock(child, 7m, "wh-a", uom: "EA");

        var r = await RunOk(s, root, RecallTraceDirection.Forward);

        Assert.Equal(3, r.Summary.Stock.Count);
        Assert.Contains(r.Summary.Stock, q => q.SkuId == s.Sku && q.BaseUomId == "EA" && q.Quantity == 10m);
        Assert.Contains(r.Summary.Stock, q => q.SkuId == s.Sku && q.BaseUomId == "KG" && q.Quantity == 2.5m);
        Assert.Contains(r.Summary.Stock, q => q.SkuId == other && q.BaseUomId == "EA" && q.Quantity == 7m);
    }

    // ── CT-R8 company rule ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Other_company_activity_is_one_count_without_lot_quantity_or_document()
    {
        var (s, _, _, bulk, _) = Golden();
        s.OtherCompany = 3;

        var r = await RunOk(s, bulk);

        var gap = Assert.Single(r.Gaps, g => g.GapCode == TraceGapCode.OTHER_COMPANY_ACTIVITY);
        Assert.Equal(3, gap.Count);
        Assert.Null(gap.LotId);
        Assert.Null(gap.LotNumber);
        Assert.Null(gap.Quantity);
        Assert.Null(gap.Movement);
        Assert.Null(gap.SkuId);
        Assert.Equal(3, r.Summary.OtherCompanyActivityCount);
    }

    // ── shipment read (pack §8.6) ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ShipmentReadOutcome.Forbidden)]
    [InlineData(ShipmentReadOutcome.NotFound)]
    [InlineData(ShipmentReadOutcome.Unavailable)]
    public async Task Shipment_read_degrades_and_the_trace_continues_with_the_same_seal(ShipmentReadOutcome outcome)
    {
        var (s, _, _, bulk, pack) = Golden();
        var ok = await RunOk(s, bulk, reader: new FakeShipmentReader());
        var degraded = await RunOk(s, bulk, reader: new FakeShipmentReader(outcome));

        Assert.Equal(ok.Sha256, degraded.Sha256);
        Assert.Equal(ok.Nodes.Select(n => n.NodeKey), degraded.Nodes.Select(n => n.NodeKey));
        var shipment = Node(degraded, TraceRules.ShipmentKey(Id(pack), Shp1));
        Assert.Equal(outcome, shipment.Shipment!.Read);
        Assert.Equal("DOC:" + Shp1, shipment.CustomerRef);
        Assert.Equal("CUST-0b5e", Node(ok, TraceRules.ShipmentKey(Id(pack), Shp1)).CustomerRef);
    }

    [Fact]
    public async Task Only_MVP6_shipments_are_read_and_quantities_come_from_the_ledger_never_from_shipment_lines()
    {
        var (s, _, _, bulk, pack) = Golden();
        s.Move(MovementType.GoodsIssueSales, Id(pack), 1m, "SO-LEGACY-1", "MOD-0172", 9);
        var reader = new FakeShipmentReader();

        var r = await RunOk(s, bulk, reader: reader);

        Assert.Equal(new[] { Shp1, Shp2 }, reader.Asked.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(300m, Node(r, TraceRules.ShipmentKey(Id(pack), Shp1)).Quantity);
        // the read model has no quantity at all: effectiveness (R3+) can never take a shipment line quantity from it
        Assert.DoesNotContain(typeof(TraceShipmentInfo).GetProperties(), p => p.Name.Contains("Quantity", StringComparison.OrdinalIgnoreCase));
    }

    // ── determinism + golden seal ─────────────────────────────────────────────────────────────────────────

    /// <summary>The <c>rt-v1</c> seal of the golden graph — pinned; any change of the canonical form or of the rule-10 order breaks it.</summary>
    public const string GoldenSha256 = "7318b1d8d9c94a648c45672d5b67c3be28b5c227dbbd29f59c58e59a30e9133a";

    [Fact]
    public async Task Golden_graph_seal_is_pinned_and_independent_of_read_order()
    {
        var (s, _, _, bulk, _) = Golden();
        var first = await RunOk(s, bulk);
        var second = await RunOk(s, bulk);
        s.Shuffle = true;
        var shuffled = await RunOk(s, bulk);
        var shuffledAgain = await RunOk(s, bulk);

        Assert.Equal(TraceRules.CanonicalVersion, first.CanonicalVersion);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.Sha256, shuffled.Sha256);
        Assert.Equal(first.Sha256, shuffledAgain.Sha256);
        Assert.Equal(first.Nodes.Select(n => n.NodeKey), shuffled.Nodes.Select(n => n.NodeKey));
        Assert.True(GoldenSha256 == first.Sha256, $"the golden rt-v1 seal changed: {first.Sha256}");
    }

    [Fact]
    public async Task Node_order_is_rule_10_root_then_backward_then_forward_by_level()
    {
        var (s, _, _, bulk, _) = Golden();
        var r = await RunOk(s, bulk);

        Assert.Equal(L(bulk), r.Nodes[0].NodeKey);
        var sides = r.Nodes.Select(n => ((int)n.Side, n.Level)).ToList();
        Assert.Equal(sides.OrderBy(x => x.Item1).ThenBy(x => x.Level), sides);
    }
}
