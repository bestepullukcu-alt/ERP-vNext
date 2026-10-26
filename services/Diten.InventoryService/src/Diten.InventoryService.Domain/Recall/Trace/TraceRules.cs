using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;
using WireNames = Diten.InventoryService.Domain.Rules.WireNames;

namespace Diten.InventoryService.Domain.Recall.Trace;

/// <summary>
/// Pack §8.2 — the pure rules of the trace: node keys (shared with the scope items, <see cref="RecallScopeRules.NodeKey"/>), movement
/// classes, the lot kind, the determinism order (rule 10), the summary and the canonical form <c>rt-v1</c> whose SHA-256 seals a trace
/// (pack §8.3). No I/O.
/// </summary>
public static class TraceRules
{
    public const string CanonicalVersion = "rt-v1";

    /// <summary>The MVP-6 shipment module: a GOODS_ISSUE_SALES of this source names a shipment (pack §8.2 step 6; read, never a decision).</summary>
    public const string ShipmentModule = "MOD-0183";

    /// <summary>Step 6 — the outbound types of the trace. WAREHOUSE_TRANSFER is internal: the stock reappears as a Stock node at the destination.</summary>
    public static readonly IReadOnlySet<MovementType> Outbound = new HashSet<MovementType>
    {
        MovementType.GoodsIssueSales, MovementType.GoodsIssueProd, MovementType.SupplierReturn, MovementType.Scrap, MovementType.WriteOff
    };

    public static readonly IReadOnlySet<MovementType> Disposal = new HashSet<MovementType>
    {
        MovementType.SupplierReturn, MovementType.Scrap, MovementType.WriteOff
    };

    public static readonly IReadOnlySet<MovementType> Inbound = new HashSet<MovementType>
    {
        MovementType.OpeningBalance, MovementType.GoodsReceiptPo, MovementType.GoodsReceiptProd, MovementType.CustomerReturn, MovementType.CountGain
    };

    /// <summary>The kind of a lot node from its first inbound movement (pack §8.2: ProductionReceipt → finished good, GoodsReceipt → raw material).</summary>
    public static TraceNodeKind LotKind(MovementType? firstInbound) => firstInbound switch
    {
        MovementType.GoodsReceiptProd => TraceNodeKind.FinishedGoodLot,
        MovementType.GoodsReceiptPo => TraceNodeKind.RawMaterialLot,
        _ => TraceNodeKind.Lot
    };

    public static bool IsLotKind(TraceNodeKind kind) => kind is TraceNodeKind.RawMaterialLot or TraceNodeKind.FinishedGoodLot or TraceNodeKind.Lot;

    // ── node keys (Lot / Stock / Shipment keys are the scope-item keys) ────────────────────────────────────

    public static string LotKey(string lotId) => RecallScopeRules.NodeKey(RecallScopeItemKind.Lot, lotId);

    public static string StockKey(string lotId, string? warehouseId, string? locationId, StockStatus status)
        => RecallScopeRules.NodeKey(RecallScopeItemKind.Stock, lotId, warehouseId, locationId, WireNames.ToWire(status));

    public static string ShipmentKey(string lotId, string documentId) => RecallScopeRules.NodeKey(RecallScopeItemKind.Shipment, lotId, sourceDocumentId: documentId);

    public static string SourceInputKey(string lotId) => $"Input:{lotId}:SRC";

    public static string UntrackedInputKey(string lotId, Guid skuId) => $"Input:{lotId}:SKU:{skuId:D}";

    public static string DisposalKey(string lotId, MovementType type, string documentOrTransaction)
        => $"Disposal:{lotId}:{WireNames.ToWire(type)}:{documentOrTransaction}";

    public static string GapKey(TraceGapCode code, string reference) => $"Gap:{code}:{reference}";

    // ── determinism (rule 10) ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root, then the backward closure, then the forward closure; within each by level, kind order, lot number, warehouse id, document number,
    /// node key (ordinal). The same data gives the same list (and the same seal).
    /// </summary>
    public static List<TraceNode> Order(IEnumerable<TraceNode> nodes)
        => nodes.OrderBy(n => (int)n.Side)
            .ThenBy(n => n.Level)
            .ThenBy(n => (int)n.Kind)
            .ThenBy(n => n.LotNumber ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(n => n.WarehouseId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(n => n.Movement?.SourceDocumentId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(n => n.NodeKey, StringComparer.Ordinal)
            .ToList();

    // ── summary ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Per SKU + base unit — quantities of different SKUs or units are never added together.</summary>
    public static TraceSummary Summarize(IReadOnlyList<TraceNode> nodes)
    {
        static IReadOnlyList<TraceQuantity> PerUnit(IEnumerable<TraceNode> of)
            => of.Where(n => n.SkuId is not null && n.BaseUomId is not null && n.Quantity is not null)
                .GroupBy(n => (n.SkuId!.Value, n.BaseUomId!))
                .OrderBy(g => g.Key.Value).ThenBy(g => g.Key.Item2, StringComparer.Ordinal)
                .Select(g => new TraceQuantity(g.Key.Value, g.Key.Item2, g.Sum(n => n.Quantity!.Value), g.Count()))
                .ToList();

        var shipments = nodes.Where(n => n.Kind == TraceNodeKind.Shipment).ToList();
        return new TraceSummary(
            nodes.Count(n => n.Kind == TraceNodeKind.FinishedGoodLot),
            PerUnit(nodes.Where(n => n.Kind == TraceNodeKind.Stock)),
            PerUnit(shipments),
            shipments.Count(s => s.Shipment is { Read: ShipmentReadOutcome.Ok, Status: "InTransit" or "Dispatched" }),
            nodes.Count(n => n.Kind == TraceNodeKind.Gap),
            nodes.Where(n => n.GapCode == TraceGapCode.OTHER_COMPANY_ACTIVITY).Sum(n => n.Count ?? 0));
    }

    // ── canonical form + seal (pack §8.3) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>rt-v1</c>: the ledger + genealogy facts of the trace in the rule-10 order; decimals as invariant strings (trailing zeros dropped);
    /// edges sorted. NOT in the seal: the shipment read (status, delivery time, ship-to — MVP-6 display, it varies with reachability),
    /// the customer reference derived from it, the compute time and duration.
    /// </summary>
    public static string Canonical(string rootLotId, string direction, bool truncated, IReadOnlyList<TraceNode> orderedNodes, IReadOnlyList<TraceEdge> edges)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("form", CanonicalVersion);
            w.WriteString("rootLotId", rootLotId);
            w.WriteString("direction", direction);
            w.WriteBoolean("truncated", truncated);
            w.WriteStartArray("nodes");
            foreach (var n in orderedNodes)
            {
                w.WriteStartObject();
                w.WriteString("key", n.NodeKey);
                Str(w, "parent", n.ParentKey);
                w.WriteString("side", n.Side.ToString());
                w.WriteNumber("level", n.Level);
                w.WriteString("kind", n.Kind.ToString());
                Str(w, "lotId", n.LotId);
                Str(w, "lotNumber", n.LotNumber);
                Str(w, "skuId", n.SkuId?.ToString("D"));
                w.WriteBoolean("untracked", n.Untracked);
                Str(w, "warehouseId", n.WarehouseId);
                Str(w, "locationId", n.LocationId);
                Str(w, "stockStatus", n.StockStatus is { } s ? WireNames.ToWire(s) : null);
                Str(w, "quantity", n.Quantity is { } q ? Dec(q) : null);
                Str(w, "baseUomId", n.BaseUomId);
                Str(w, "movementType", n.Movement?.MovementType);
                Str(w, "documentId", n.Movement?.SourceDocumentId);
                Str(w, "gap", n.GapCode?.ToString());
                Str(w, "count", n.Count?.ToString(CultureInfo.InvariantCulture));
                w.WriteStartArray("serials");
                foreach (var serial in n.Serials.OrderBy(x => x, StringComparer.Ordinal)) w.WriteStringValue(serial);
                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("edges");
            foreach (var e in edges.OrderBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal)
                         .ThenBy(e => e.TransformationReference ?? string.Empty, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("from", e.From);
                w.WriteString("to", e.To);
                Str(w, "ref", e.TransformationReference);
                Str(w, "qty", e.QuantityConsumed);
                Str(w, "uom", e.UomId);
                w.WriteBoolean("cycle", e.Cycle);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string Sha256(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

    public static string Dec(decimal value) => (value / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    private static void Str(Utf8JsonWriter w, string name, string? value)
    {
        if (value is null) w.WriteNull(name);
        else w.WriteString(name, value);
    }
}
