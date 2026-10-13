using System.Globalization;
using Diten.Web.Models.Inventory;

namespace Diten.Web.Models.Traceability;

/// <summary>One card of the İzlenebilirlik flow: a lot (clickable, re-roots the flow) or a document (receipt / shipment).</summary>
public sealed record TraceNode(string Kind, string? LotId, string Title, string? Subtitle, string? Status, string? Reference);

/// <summary>"Etkilenen toplam miktar" for one unit (pack §11.3: per unit, NEVER summed across units).</summary>
public sealed record TraceTotal(string BaseUomId, string OnHand, string Shipped, string Affected);

public sealed record LotTraceView(
    IReadOnlyList<TraceNode> Back1,
    IReadOnlyList<TraceNode> Back2,
    IReadOnlyList<TraceNode> Forward1,
    IReadOnlyList<TraceNode> Forward2,
    IReadOnlyList<TraceTotal> Totals,
    int AffectedLots);

/// <summary>
/// MOD-0174 P3 İzlenebilirlik (pack §11.3), MVP-1 reality: backward = the lot's genealogy parents (2 levels) + its own
/// receipt / opening source; forward = genealogy children (2 levels) + the outbound ledger documents of the lot shown as
/// document NUMBERS only (no customer name until MVP-6). Quantities in decimal, grouped by base unit.
/// </summary>
public static class LotTraceFlow
{
    /// <summary>Ledger types that send the lot's stock out of the company (a shipment) or into production (consumed).</summary>
    public static readonly IReadOnlySet<string> Shipment = new HashSet<string>(StringComparer.Ordinal) { "GOODS_ISSUE_SALES" };

    public static readonly IReadOnlySet<string> Consumption = new HashSet<string>(StringComparer.Ordinal) { "GOODS_ISSUE_PROD" };

    public static LotTraceView Build(
        LotDetailDto root,
        GenealogyDto? genealogy,
        IReadOnlyDictionary<string, LotRefDto> relatedLots,
        IReadOnlyList<TransactionItemDto> rootTransactions)
    {
        var rootId = root.LotId ?? string.Empty;
        var parents = genealogy?.Parents ?? [];
        var children = genealogy?.Children ?? [];

        TraceNode LotNode(string? id, string kind, string? reference)
        {
            relatedLots.TryGetValue(id ?? string.Empty, out var lot);
            return new TraceNode(kind, id, lot?.LotNumber ?? id ?? string.Empty, lot?.ExpiryDate, lot?.Status, reference);
        }

        var back1 = parents.Where(p => p.ChildLotId == rootId).Select(p => LotNode(p.ParentLotId, "lot", p.TransformationReference)).ToList();
        var parentIds = back1.Select(n => n.LotId).ToHashSet(StringComparer.Ordinal);
        var back2 = parents.Where(p => p.ChildLotId != rootId && parentIds.Contains(p.ChildLotId)).Select(p => LotNode(p.ParentLotId, "lot", p.TransformationReference)).ToList();
        if (root.Source is { } source)
        {
            var kind = source.Kind == "OpeningBalance" ? "opening" : "receipt";
            back1.Add(new TraceNode(kind, null, source.DocumentNumber ?? string.Empty, source.Date, null, source.Kind));
        }

        var forward1 = children.Where(c => c.ParentLotId == rootId).Select(c => LotNode(c.ChildLotId, "lot", c.TransformationReference)).ToList();
        var childIds = forward1.Select(n => n.LotId).ToHashSet(StringComparer.Ordinal);
        var forward2 = children.Where(c => c.ParentLotId != rootId && childIds.Contains(c.ParentLotId)).Select(c => LotNode(c.ChildLotId, "lot", c.TransformationReference)).ToList();
        forward1.AddRange(rootTransactions
            .Where(t => Shipment.Contains(t.MovementType ?? string.Empty) || Consumption.Contains(t.MovementType ?? string.Empty))
            .Select(t => new TraceNode(Shipment.Contains(t.MovementType!) ? "shipment" : "consumed", null,
                t.SourceDocumentId ?? t.TransactionNumber ?? t.TransactionId ?? string.Empty,
                t.TransactionDate, null, t.TransactionNumber)));

        var totals = new Dictionary<string, (decimal OnHand, decimal Shipped)>(StringComparer.Ordinal);
        if (root.StockSummary is { BaseUomId: { } uom } summary && TryDecimal(summary.OnHand, out var onHand))
            totals[uom] = (onHand, 0m);
        foreach (var t in rootTransactions.Where(t => Shipment.Contains(t.MovementType ?? string.Empty)))
        {
            if (t.BaseUomId is not { } unit || !TryDecimal(t.BaseQuantity, out var q)) continue;
            totals.TryGetValue(unit, out var current);
            totals[unit] = (current.OnHand, current.Shipped + q);
        }

        var affectedLots = new HashSet<string>(StringComparer.Ordinal) { rootId };
        foreach (var n in back1.Concat(back2).Concat(forward1).Concat(forward2).Where(n => n.LotId is not null)) affectedLots.Add(n.LotId!);

        return new LotTraceView(back1, back2, forward1, forward2,
            totals.OrderBy(t => t.Key, StringComparer.Ordinal)
                .Select(t => new TraceTotal(t.Key, Wire(t.Value.OnHand), Wire(t.Value.Shipped), Wire(t.Value.OnHand + t.Value.Shipped)))
                .ToList(),
            affectedLots.Count);
    }

    private static bool TryDecimal(string? value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);

    private static string Wire(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
