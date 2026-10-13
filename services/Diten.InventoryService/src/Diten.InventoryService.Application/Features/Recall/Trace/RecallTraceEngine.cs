using System.Diagnostics;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Trace;

namespace Diten.InventoryService.Application.Features.Recall.Trace;

/// <summary>The parameters of one trace (pack §8.2 input).</summary>
public sealed record TraceRequest(Guid RootLotId, RecallTraceDirection Direction, DateTimeOffset AsOfUtc, int MaxDepth, int MaxNodes);

/// <summary>
/// Pack §8.2 — the trace engine. A level-by-level walk over in-process reads (<see cref="IRecallTraceSource"/>: one batched query per
/// level, never the 2-level HTTP <c>getGenealogy</c>), with a visited set (a loop is an edge marked <c>cycle</c>, never a second visit),
/// the depth / node limits (reaching one is <c>truncated</c> + a <c>LIMIT_REACHED</c> gap — never silent), stock and outbound
/// movements of the root and the forward closure in the caller's company, the gap rules, "Takip dışı" inputs (not gaps), serials, the
/// count of other-company activity (CT-R8), and the MVP-6 shipment read (pack §8.6, degrading — the trace always completes). The result
/// is ordered by rule 10 and sealed (<c>rt-v1</c>).
/// </summary>
public sealed class RecallTraceEngine
{
    private readonly IRecallTraceSource _source;
    private readonly IShipmentReader _shipments;

    public RecallTraceEngine(IRecallTraceSource source, IShipmentReader shipments)
    {
        _source = source;
        _shipments = shipments;
    }

    /// <summary>null = the root lot is not a lot of the caller's tenant.</summary>
    public async Task<TraceResult?> RunAsync(TraceRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var root = (await _source.LotsAsync([request.RootLotId], ct)).SingleOrDefault();
        if (root is null) return null;

        var run = new Run(request);
        run.Reserve(); // the root takes the first place of the node budget
        var lots = new Dictionary<Guid, TraceLot> { [root.Id] = root };
        var levelOf = new Dictionary<Guid, (TraceSide Side, int Level, string? Parent)> { [root.Id] = (TraceSide.Root, 0, null) };
        var parentLinks = new Dictionary<Guid, List<TraceLink>>();
        var childLinks = new Dictionary<Guid, List<TraceLink>>();
        var backward = new List<Guid> { root.Id };
        var forward = new List<Guid> { root.Id };

        if (request.Direction is RecallTraceDirection.Backward or RecallTraceDirection.Both)
        {
            backward = await WalkAsync(run, root.Id, TraceSide.Backward, lots, levelOf, parentLinks, ct);
        }

        if (request.Direction is RecallTraceDirection.Forward or RecallTraceDirection.Both)
        {
            forward = await WalkAsync(run, root.Id, TraceSide.Forward, lots, levelOf, childLinks, ct);
        }

        // lot nodes, their kind from the first inbound movement in the company (pack §8.2: production receipt → finished good …)
        var inbound = await _source.FirstInboundAsync(lots.Keys.ToList(), ct);
        foreach (var lot in lots.Values.OrderBy(l => l.Id))
        {
            var (side, level, parent) = levelOf[lot.Id];
            run.Add(new TraceNode
            {
                NodeKey = TraceRules.LotKey(lot.Id.ToString("D")),
                ParentKey = parent,
                Side = side,
                Level = level,
                Kind = TraceRules.LotKind(inbound.TryGetValue(lot.Id, out var first) ? first.Type : null),
                LotId = lot.Id.ToString("D"),
                LotNumber = lot.LotNumber,
                ItemId = lot.ItemId,
                SkuId = lot.SkuId,
                SkuLevel = lot.SkuLevel
            }, force: true);
        }

        if (request.Direction is RecallTraceDirection.Backward or RecallTraceDirection.Both)
        {
            await InputsAsync(run, backward, lots, levelOf, parentLinks, inbound, ct);
        }

        var forwardLots = forward.Distinct().ToList();
        var lotControlled = new Dictionary<(Guid, SkuLevel), bool?>();
        await StockAsync(run, forwardLots, lots, levelOf, ct);
        await OutboundAsync(run, forwardLots, lots, levelOf, childLinks, inbound, lotControlled, request.AsOfUtc, ct);

        // CT-R8: other companies are ONE count — never their lots, quantities or documents
        var others = await _source.OtherCompanyActivityAsync(lots.Keys.ToList(), ct);
        if (others > 0)
        {
            run.Add(new TraceNode
            {
                NodeKey = TraceRules.GapKey(TraceGapCode.OTHER_COMPANY_ACTIVITY, "tenant"),
                Kind = TraceNodeKind.Gap,
                GapCode = TraceGapCode.OTHER_COMPANY_ACTIVITY,
                Side = TraceSide.Root,
                Count = others,
                Text = $"{others} movement(s) of these lots are in another company of the tenant; run the trace there."
            }, force: true);
        }

        await ShipmentsAsync(run, ct);

        if (run.Truncated)
        {
            run.Add(new TraceNode
            {
                NodeKey = TraceRules.GapKey(TraceGapCode.LIMIT_REACHED, "limit"),
                Kind = TraceNodeKind.Gap,
                GapCode = TraceGapCode.LIMIT_REACHED,
                Side = TraceSide.Root,
                Text = $"The trace stopped at its limit (depth {request.MaxDepth}, nodes {request.MaxNodes}); the result is incomplete."
            }, force: true);
        }

        var ordered = TraceRules.Order(run.Nodes.Values);
        var direction = request.Direction.ToString();
        var canonical = TraceRules.Canonical(root.Id.ToString("D"), direction, run.Truncated, ordered, run.Edges);
        clock.Stop();
        return new TraceResult(root.Id.ToString("D"), root.LotNumber, direction, request.AsOfUtc, now, clock.ElapsedMilliseconds, run.Truncated,
            request.MaxDepth, request.MaxNodes, ordered, run.Edges, TraceRules.Summarize(ordered), TraceRules.CanonicalVersion, TraceRules.Sha256(canonical));
    }

    // ── genealogy walk (steps 2 / 3) ───────────────────────────────────────────────────────────────────────

    private async Task<List<Guid>> WalkAsync(Run run, Guid root, TraceSide side, Dictionary<Guid, TraceLot> lots,
        Dictionary<Guid, (TraceSide Side, int Level, string? Parent)> levelOf, Dictionary<Guid, List<TraceLink>> linksOf, CancellationToken ct)
    {
        var visited = new Dictionary<Guid, int> { [root] = 0 };
        var reached = new List<Guid> { root };
        var frontier = new List<Guid> { root };
        var level = 0;
        while (frontier.Count > 0)
        {
            var links = side == TraceSide.Backward ? await _source.ParentLinksAsync(frontier, ct) : await _source.ChildLinksAsync(frontier, ct);
            foreach (var group in links.GroupBy(l => side == TraceSide.Backward ? l.ChildLotId : l.ParentLotId))
            {
                linksOf[group.Key] = group.ToList();
            }

            if (links.Count == 0) break;
            if (level >= run.Request.MaxDepth)
            {
                run.Truncated = true; // more genealogy exists beyond the depth limit
                break;
            }

            var next = new List<Guid>();
            foreach (var link in links.OrderBy(l => l.ParentLotId).ThenBy(l => l.ChildLotId).ThenBy(l => l.TransformationReference, StringComparer.Ordinal))
            {
                var from = side == TraceSide.Backward ? link.ChildLotId : link.ParentLotId;
                var other = side == TraceSide.Backward ? link.ParentLotId : link.ChildLotId;
                var seen = visited.TryGetValue(other, out var seenAt);
                run.Edges.Add(new TraceEdge(TraceRules.LotKey(link.ParentLotId.ToString("D")), TraceRules.LotKey(link.ChildLotId.ToString("D")),
                    link.TransformationReference, link.QuantityConsumed, link.UomId, Cycle: seen && seenAt <= level));
                if (seen) continue;
                if (!run.Reserve())
                {
                    continue; // node limit: truncated, the gap is written at the end
                }

                visited[other] = level + 1;
                next.Add(other);
                reached.Add(other);
                if (!levelOf.ContainsKey(other)) levelOf[other] = (side, level + 1, TraceRules.LotKey(from.ToString("D")));
            }

            if (next.Count > 0)
            {
                foreach (var lot in await _source.LotsAsync(next.Where(id => !lots.ContainsKey(id)).ToList(), ct)) lots[lot.Id] = lot;
                next.RemoveAll(id => !lots.ContainsKey(id)); // a link to a lot that does not exist for the tenant is not followed
            }

            frontier = next;
            level++;
        }

        return reached.Where(lots.ContainsKey).ToList();
    }

    // ── backward inputs (steps 2 / 4) ─────────────────────────────────────────────────────────────────────

    private async Task InputsAsync(Run run, IReadOnlyList<Guid> backward, Dictionary<Guid, TraceLot> lots,
        Dictionary<Guid, (TraceSide Side, int Level, string? Parent)> levelOf, Dictionary<Guid, List<TraceLink>> parentLinks,
        IReadOnlyDictionary<Guid, TraceTransaction> inbound, CancellationToken ct)
    {
        foreach (var lotId in backward.Distinct().OrderBy(x => x))
        {
            if (!inbound.TryGetValue(lotId, out var source)) continue;
            var lot = lots[lotId];
            var lotKey = TraceRules.LotKey(lotId.ToString("D"));
            var level = levelOf[lotId].Level + 1;
            var parents = parentLinks.TryGetValue(lotId, out var p) ? p : [];
            if (parents.Count == 0)
            {
                run.Add(new TraceNode
                {
                    NodeKey = TraceRules.SourceInputKey(lotId.ToString("D")), ParentKey = lotKey, Side = TraceSide.Backward, Level = level,
                    Kind = TraceNodeKind.Input, LotId = lotId.ToString("D"), LotNumber = lot.LotNumber, ItemId = lot.ItemId, SkuId = lot.SkuId,
                    SkuLevel = lot.SkuLevel, Quantity = source.BaseQuantity, BaseUomId = source.BaseUomId, Movement = Ref(source)
                });
            }

            // step 4: the inputs issued to the production document of a produced lot
            if (source.Type != MovementType.GoodsReceiptProd || string.IsNullOrEmpty(source.SourceDocumentId)) continue;
            var parentIds = parents.Select(l => l.ParentLotId.ToString("D")).ToHashSet(StringComparer.Ordinal);
            var issues = Net(await _source.TransactionsOfDocumentAsync(source.SourceModule, source.SourceDocumentId, ct))
                .Where(t => t.Type == MovementType.GoodsIssueProd).ToList();
            foreach (var group in issues.GroupBy(t => (t.SkuId, t.SkuLevel, t.ItemId, Lot: Guid.TryParseExact(t.LotId, "D", out _) ? t.LotId : null)))
            {
                var controlled = await _source.LotControlledAsync(group.Key.SkuId, group.Key.SkuLevel, ct);
                if (group.Key.Lot is { } inputLot)
                {
                    if (parentIds.Contains(inputLot)) continue; // the genealogy link is there
                    run.Add(Gap(TraceGapCode.MISSING_GENEALOGY_LINK, $"{lotId:D}:{inputLot}", lotKey, TraceSide.Backward, level, group.First(),
                        group.Sum(t => t.BaseQuantity), $"Lot {inputLot} was issued to {source.SourceDocumentId} but has no genealogy link to {lot.LotNumber}."));
                }
                else if (controlled == false)
                {
                    // "Takip dışı" — a material that is not lot-controlled: an input, NOT a gap (KR-D)
                    run.Add(new TraceNode
                    {
                        NodeKey = TraceRules.UntrackedInputKey(lotId.ToString("D"), group.Key.SkuId), ParentKey = lotKey, Side = TraceSide.Backward,
                        Level = level, Kind = TraceNodeKind.Input, Untracked = true, ItemId = group.Key.ItemId, SkuId = group.Key.SkuId,
                        SkuLevel = group.Key.SkuLevel, Quantity = group.Sum(t => t.BaseQuantity), BaseUomId = group.First().BaseUomId, Movement = Ref(group.First())
                    });
                }
                else
                {
                    run.Add(Gap(TraceGapCode.MISSING_GENEALOGY_LINK, $"{lotId:D}:SKU:{group.Key.SkuId:D}", lotKey, TraceSide.Backward, level, group.First(),
                        group.Sum(t => t.BaseQuantity), $"A lot-controlled input was issued to {source.SourceDocumentId} without a lot."));
                }
            }
        }
    }

    // ── stock (step 5) ────────────────────────────────────────────────────────────────────────────────────

    private async Task StockAsync(Run run, IReadOnlyList<Guid> forward, Dictionary<Guid, TraceLot> lots,
        Dictionary<Guid, (TraceSide Side, int Level, string? Parent)> levelOf, CancellationToken ct)
    {
        var balances = await _source.BalancesAsync(forward.Select(l => lots[l].ItemId).Distinct().ToList(), forward, ct);
        var serialLots = forward.Where(l => lots[l].Serialized).ToList();
        var serials = serialLots.Count == 0 ? [] : await _source.SerialsAsync(serialLots, ct);
        foreach (var group in balances.GroupBy(b => (b.LotId, b.WarehouseId, b.LocationId, b.Status, b.BaseUomId))
                     .OrderBy(g => g.Key.LotId, StringComparer.Ordinal).ThenBy(g => g.Key.WarehouseId, StringComparer.Ordinal)
                     .ThenBy(g => g.Key.LocationId, StringComparer.Ordinal).ThenBy(g => g.Key.Status))
        {
            var lotId = Guid.Parse(group.Key.LotId);
            var lot = lots[lotId];
            var (_, level, _) = levelOf[lotId];
            run.Add(new TraceNode
            {
                NodeKey = TraceRules.StockKey(group.Key.LotId, group.Key.WarehouseId, group.Key.LocationId, group.Key.Status),
                ParentKey = TraceRules.LotKey(group.Key.LotId), Side = TraceSide.Forward, Level = level + 1,
                Kind = TraceNodeKind.Stock, LotId = group.Key.LotId, LotNumber = lot.LotNumber, ItemId = lot.ItemId, SkuId = lot.SkuId, SkuLevel = lot.SkuLevel,
                WarehouseId = group.Key.WarehouseId, LocationId = group.Key.LocationId, StockStatus = group.Key.Status, Quantity = group.Sum(b => b.OnHand),
                BaseUomId = group.Key.BaseUomId,
                Serials = serials.Where(s => s.Active && s.LotId == lotId && s.WarehouseId == group.Key.WarehouseId
                                             && (group.Key.LocationId is null || s.LocationId == group.Key.LocationId)).Select(s => s.Number).ToList()
            });
        }
    }

    // ── outbound movements + gap rules (steps 6 / 8) ─────────────────────────────────────────────────────

    private async Task OutboundAsync(Run run, IReadOnlyList<Guid> forward, Dictionary<Guid, TraceLot> lots,
        Dictionary<Guid, (TraceSide Side, int Level, string? Parent)> levelOf, Dictionary<Guid, List<TraceLink>> childLinks,
        IReadOnlyDictionary<Guid, TraceTransaction> inbound, Dictionary<(Guid, SkuLevel), bool?> lotControlled, DateTimeOffset asOf, CancellationToken ct)
    {
        var lotSet = forward.ToHashSet();
        var movements = Net(await _source.TransactionsOfLotsAsync(forward, ct)).Where(t => t.PostingDate <= asOf).ToList();

        // step 8: lotless / typed-lot outbound of the closure's lot-controlled SKUs in the window of their receipts
        foreach (var sku in forward.Select(l => lots[l]).GroupBy(l => (l.ItemId, l.SkuId, l.SkuLevel)))
        {
            if (!lotControlled.TryGetValue((sku.Key.SkuId, sku.Key.SkuLevel), out var controlled))
            {
                controlled = lotControlled[(sku.Key.SkuId, sku.Key.SkuLevel)] = await _source.LotControlledAsync(sku.Key.SkuId, sku.Key.SkuLevel, ct);
            }

            if (controlled == false) continue;
            var starts = sku.Where(l => inbound.ContainsKey(l.Id)).Select(l => inbound[l.Id].PostingDate).ToList();
            if (starts.Count == 0) continue;
            var window = Net(await _source.OutboundOfSkuAsync(sku.Key.ItemId, sku.Key.SkuId, starts.Min(), asOf, ct))
                .Where(t => t.SkuLevel == sku.Key.SkuLevel && TraceRules.Outbound.Contains(t.Type)).ToList();
            var typed = window.Where(t => t.LotId is not null && !Guid.TryParseExact(t.LotId, "D", out _)).ToList();
            var typedLinks = await _source.TypedLotLinksAsync(typed.Select(t => t.Id).ToList(), ct);
            var root = TraceRules.LotKey(run.Request.RootLotId.ToString("D"));
            foreach (var t in window.Where(t => t.LotId is null))
            {
                run.Add(Gap(TraceGapCode.LOTLESS_OUTBOUND, t.Id, root, TraceSide.Forward, 1, t, t.BaseQuantity,
                    $"Outbound {t.Number ?? t.Id} of a lot-controlled SKU has no lot ({t.SourceDocumentId ?? "no document"})."));
            }

            foreach (var t in typed)
            {
                if (typedLinks.TryGetValue(t.Id, out var linked))
                {
                    if (lotSet.Contains(linked) && movements.All(m => m.Id != t.Id)) movements.Add(t with { LotId = linked.ToString("D") });
                    continue;
                }

                run.Add(Gap(TraceGapCode.UNRESOLVED_TYPED_LOT, t.Id, root, TraceSide.Forward, 1, t, t.BaseQuantity,
                    $"Outbound {t.Number ?? t.Id} carries the typed lot text '{t.LotId}' that the migration did not link to a lot."));
            }
        }

        foreach (var group in movements.Where(t => TraceRules.Outbound.Contains(t.Type))
                     .GroupBy(t => (t.LotId!, t.Type, Doc: t.Type == MovementType.GoodsIssueSales ? t.SourceDocumentId ?? string.Empty : t.Id))
                     .OrderBy(g => g.Key.Item1, StringComparer.Ordinal).ThenBy(g => g.Key.Type).ThenBy(g => g.Key.Doc, StringComparer.Ordinal))
        {
            var lotId = Guid.Parse(group.Key.Item1);
            var lot = lots[lotId];
            var lotKey = TraceRules.LotKey(group.Key.Item1);
            var level = levelOf[lotId].Level + 1;
            var first = group.OrderBy(t => t.PostingDate).ThenBy(t => t.Id, StringComparer.Ordinal).First();
            var quantity = group.Sum(t => t.BaseQuantity);
            switch (group.Key.Type)
            {
                case MovementType.GoodsIssueSales:
                {
                    var document = string.IsNullOrEmpty(first.SourceDocumentId) ? null : first.SourceDocumentId;
                    var node = new TraceNode
                    {
                        NodeKey = TraceRules.ShipmentKey(group.Key.Item1, document ?? "TXN-" + first.Id), ParentKey = lotKey, Side = TraceSide.Forward,
                        Level = level, Kind = TraceNodeKind.Shipment, LotId = group.Key.Item1, LotNumber = lot.LotNumber, ItemId = lot.ItemId, SkuId = lot.SkuId,
                        SkuLevel = lot.SkuLevel, Quantity = quantity, BaseUomId = first.BaseUomId, Movement = Ref(first),
                        Serials = group.SelectMany(t => t.SerialIds).Distinct(StringComparer.Ordinal).ToList(),
                        Shipment = new TraceShipmentInfo(ShipmentReadOutcome.NotApplicable, null, null, null),
                        CustomerRef = document is null ? null : "DOC:" + document
                    };
                    run.Add(node);
                    if (document is null)
                    {
                        run.Add(Gap(TraceGapCode.NO_CUSTOMER_REFERENCE, first.Id, node.NodeKey, TraceSide.Forward, level + 1, first, quantity,
                            $"Sales issue {first.Number ?? first.Id} names no document — the customer cannot be found."));
                    }

                    break;
                }
                case MovementType.GoodsIssueProd:
                {
                    // covered by the forward genealogy (step 3); without an output link the stock left the company's sight in production
                    var outputs = childLinks.TryGetValue(lotId, out var c) ? c : [];
                    if (outputs.Any(l => string.Equals(l.TransformationReference, first.SourceDocumentId, StringComparison.Ordinal))) break;
                    run.Add(Gap(TraceGapCode.CONSUMED_WITHOUT_OUTPUT_LINK, first.Id, lotKey, TraceSide.Forward, level, first, quantity,
                        $"Production issue {first.Number ?? first.Id} ({first.SourceDocumentId ?? "no document"}) has no output lot linked to {lot.LotNumber}."));
                    break;
                }
                default:
                    run.Add(new TraceNode
                    {
                        NodeKey = TraceRules.DisposalKey(group.Key.Item1, group.Key.Type, first.Id), ParentKey = lotKey, Side = TraceSide.Forward, Level = level,
                        Kind = TraceNodeKind.Disposal, LotId = group.Key.Item1, LotNumber = lot.LotNumber, ItemId = lot.ItemId, SkuId = lot.SkuId,
                        SkuLevel = lot.SkuLevel, Quantity = quantity, BaseUomId = first.BaseUomId, Movement = Ref(first)
                    });
                    break;
            }
        }
    }

    // ── the MVP-6 shipment read (pack §8.6) ──────────────────────────────────────────────────────────────

    private async Task ShipmentsAsync(Run run, CancellationToken ct)
    {
        var shipmentNodes = run.Nodes.Values.Where(n => n.Kind == TraceNodeKind.Shipment && n.Movement is { SourceModule: TraceRules.ShipmentModule, SourceDocumentId: not null })
            .ToList();
        if (shipmentNodes.Count == 0) return;
        var reads = await _shipments.ReadAsync(shipmentNodes.Select(n => n.Movement!.SourceDocumentId!).Distinct(StringComparer.Ordinal).ToList(), ct);
        foreach (var node in shipmentNodes)
        {
            var read = reads.TryGetValue(node.Movement!.SourceDocumentId!, out var r) ? r : new TraceShipmentInfo(ShipmentReadOutcome.Unavailable, null, null, null);
            node.Shipment = read;
            if (read is { Read: ShipmentReadOutcome.Ok, ShipToReference: { Length: > 0 } shipTo }) node.CustomerRef = shipTo;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The movements that stand: a REVERSAL removes its original (and is not itself a movement of the trace).</summary>
    private static List<TraceTransaction> Net(IEnumerable<TraceTransaction> rows)
    {
        var all = rows.ToList();
        var reversed = all.Where(t => t.Type == MovementType.Reversal && t.ReversesTransactionId is not null)
            .Select(t => t.ReversesTransactionId!).ToHashSet(StringComparer.Ordinal);
        return all.Where(t => t.Type != MovementType.Reversal && !reversed.Contains(t.Id)).ToList();
    }

    private static TraceMovementRef Ref(TraceTransaction t)
        => new(t.Id, t.Number, Domain.Rules.WireNames.ToWire(t.Type), t.PostingDate, t.SourceModule, t.SourceDocumentId);

    private static TraceNode Gap(TraceGapCode code, string reference, string parent, TraceSide side, int level, TraceTransaction? movement, decimal? quantity, string text)
        => new()
        {
            NodeKey = TraceRules.GapKey(code, reference), ParentKey = parent, Side = side, Level = level, Kind = TraceNodeKind.Gap, GapCode = code,
            ItemId = movement?.ItemId, SkuId = movement?.SkuId, SkuLevel = movement?.SkuLevel, Quantity = quantity, BaseUomId = movement?.BaseUomId,
            Movement = movement is null ? null : Ref(movement), Text = text
        };

    /// <summary>The nodes / edges of one run and the node budget (pack §8.2 limits).</summary>
    private sealed class Run(TraceRequest request)
    {
        private int _reserved;

        public TraceRequest Request { get; } = request;

        public Dictionary<string, TraceNode> Nodes { get; } = new(StringComparer.Ordinal);

        public List<TraceEdge> Edges { get; } = [];

        public bool Truncated { get; set; }

        /// <summary>Takes one place of the node budget; false = the limit is reached (the run is truncated).</summary>
        public bool Reserve()
        {
            if (_reserved + 1 > Request.MaxNodes)
            {
                Truncated = true;
                return false;
            }

            _reserved++;
            return true;
        }

        /// <summary>Adds a node unless its key is there; outside the walk a node also takes the budget (<paramref name="force"/>: lots
        /// already reserved by the walk, and the closing gap rows).</summary>
        public void Add(TraceNode node, bool force = false)
        {
            if (Nodes.ContainsKey(node.NodeKey)) return;
            if (!force && !Reserve()) return;
            Nodes[node.NodeKey] = node;
        }
    }
}
