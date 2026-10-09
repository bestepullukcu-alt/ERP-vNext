using System.Globalization;
using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// What the form needs to know about the chosen product / SKU (PRODUCT-MASTER-BUNDLE, contract routes). WP-0173-PM1 (v1.1,
/// OD-1): the lot / serial flags are the SKU's (<c>getSku</c>), nullable — unknown is never false. <see cref="BlockedKey"/> names
/// why the form must not send this movement (resx key): <c>ProductFlagsUnknown</c> (OD-5) or <c>ProductLevelNotStockable</c>
/// (OD-10, a Gsku of a product that is not explicitly serial-free). The service applies the same rules and decides.
/// </summary>
public sealed record ProductFacts(
    bool Available,
    bool? LotControlled,
    bool? SerialControlled,
    string? BaseUomId,
    IReadOnlyList<string> Units,
    IReadOnlyList<UomEdge> Conversions,
    string? BlockedKey = null)
{
    public static readonly ProductFacts Unavailable = new(false, null, null, null, [], []);
}

/// <summary>One declared conversion <c>fromUom → toUom = quantity × numerator / denominator</c>.</summary>
public sealed record UomEdge(string FromUom, string ToUom, decimal Numerator, decimal Denominator);

/// <summary>One balance row offered as the SOURCE of an outbound / transfer (pack §12 field 5).</summary>
public sealed record SourceOption(string Value, string Text, string WarehouseId, string LocationId, string? LotId, string StockStatus, string OnHand, string? BaseUomId);

/// <summary>The "Stock effect" panel (pack §12): before → after per side, computed here in decimal (UI-13).</summary>
public sealed record EffectSide(string Side, string Label, string Before, string After, bool Negative);

/// <summary>
/// The movement form's read sources (pack §19 lookup table). Every one is a contract read through the Gateway —
/// PRODUCT-MASTER (<c>/api/product-master</c>, FU-PM-01: no Gateway route yet, so "unavailable" until MDM exposes it),
/// INVENTORY-BUNDLE <c>/balance</c> and LOCATION — and none invents data: an unreachable source is an empty answer
/// that says so. Quantities are decimal strings in and out; the one arithmetic (base quantity, before/after) is C#
/// decimal, never browser float.
/// </summary>
public static class MovementFormSources
{
    public const int BalancePageSize = 200;

    public static async Task<ProductFacts> ProductFactsAsync(InventoryGatewayClient gateway, string itemId, string skuId, string skuLevel, CancellationToken cancellationToken)
    {
        // WP-0173-PM1: getSku (authoritative, v1.1) + getSkuUom; getProduct only at Gsku, for the product's serial flag (OD-10).
        var gsku = string.Equals(skuLevel, "Gsku", StringComparison.Ordinal);
        var skuTask = gateway.GetAsync($"/api/product-master/skus/{Uri.EscapeDataString(skuId)}?level={Uri.EscapeDataString(skuLevel)}", cancellationToken);
        var uomTask = gateway.GetAsync($"/api/product-master/skus/{Uri.EscapeDataString(skuId)}/uom", cancellationToken);
        var productTask = gsku ? gateway.GetAsync($"/api/product-master/products/{Uri.EscapeDataString(itemId)}", cancellationToken) : null;
        var replies = await Task.WhenAll(productTask is null ? new[] { skuTask, uomTask } : new[] { skuTask, uomTask, productTask });
        if (replies.Any(r => !r.IsSuccess)) return ProductFacts.Unavailable;

        try
        {
            using var sku = JsonDocument.Parse(replies[0].Body);
            using var uom = JsonDocument.Parse(replies[1].Body);
            using var product = gsku ? JsonDocument.Parse(replies[2].Body) : null;
            var baseUom = Text(uom.RootElement, "baseUomId") ?? Text(sku.RootElement, "baseUomId");
            var edges = new List<UomEdge>();
            if (uom.RootElement.TryGetProperty("conversions", out var conversions) && conversions.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in conversions.EnumerateArray())
                {
                    var from = Text(c, "fromUom");
                    var to = Text(c, "toUom");
                    if (from is null || to is null || !TryDecimal(Text(c, "numerator"), out var n) || !TryDecimal(Text(c, "denominator"), out var d)) continue;
                    edges.Add(new UomEdge(from, to, n, d));
                }
            }

            // pack §12: the base UoM + every declared fromUom — a unit that cannot reach the base is still offered, and
            // the service answers UOM_CONVERSION_MISSING for it (the hint says so first).
            var units = new List<string>();
            if (baseUom is not null) units.Add(baseUom);
            units.AddRange(edges.Select(e => e.FromUom).Where(u => !units.Contains(u, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal));

            var lot = Flag(sku.RootElement, "lotControlled");
            var serial = gsku ? (Flag(product!.RootElement, "serialControlled") == false ? false : null) : Flag(sku.RootElement, "serialControlled");
            var blocked = lot is null ? "ProductFlagsUnknown"
                : gsku && serial is null ? "ProductLevelNotStockable"
                : serial is null ? "ProductFlagsUnknown"
                : null;
            return new ProductFacts(true, lot, serial, baseUom, units, edges, blocked);
        }
        catch (JsonException)
        {
            return ProductFacts.Unavailable;
        }
    }

    /// <summary>
    /// The same walk as MOD-0173's <c>UomConverter</c> (declared directions only, ≤ 4 hops, multiply then divide once, at
    /// most 6 fraction digits) — for the HINT and the serial counter only. The service converts again and decides.
    /// </summary>
    public static string? ToBase(string quantity, string uomId, ProductFacts facts)
    {
        if (facts.BaseUomId is null || !TryDecimal(quantity, out var amount)) return null;
        if (string.Equals(uomId, facts.BaseUomId, StringComparison.Ordinal)) return Wire(amount);

        var frontier = new List<(string Uom, decimal N, decimal D)> { (uomId, 1m, 1m) };
        var visited = new HashSet<string>(StringComparer.Ordinal) { uomId };
        for (var hop = 0; hop < 4 && frontier.Count > 0; hop++)
        {
            var next = new List<(string, decimal, decimal)>();
            foreach (var (uom, n, d) in frontier)
            {
                foreach (var edge in facts.Conversions.Where(e => e.Numerator > 0m && e.Denominator > 0m && string.Equals(e.FromUom, uom, StringComparison.Ordinal)))
                {
                    if (!visited.Add(edge.ToUom)) continue;
                    try
                    {
                        var stepN = n * edge.Numerator;
                        var stepD = d * edge.Denominator;
                        if (string.Equals(edge.ToUom, facts.BaseUomId, StringComparison.Ordinal))
                        {
                            var result = amount * stepN / stepD;
                            return Scale(result) <= 6 ? Wire(result) : null;
                        }

                        next.Add((edge.ToUom, stepN, stepD));
                    }
                    catch (OverflowException)
                    {
                        return null;
                    }
                }
            }

            frontier = next;
        }

        return null;
    }

    /// <summary>The SKU's balance rows with stock on hand — the only valid sources (pack §12 field 5).</summary>
    public static async Task<(IReadOnlyList<BalanceViewDto> Rows, bool Available)> BalanceRowsAsync(
        InventoryGatewayClient gateway, string skuId, string skuLevel, CancellationToken cancellationToken, string? extraQuery = null)
    {
        var reply = await gateway.GetAsync(
            $"/api/inventory/balance?skuId={Uri.EscapeDataString(skuId)}&skuLevel={Uri.EscapeDataString(skuLevel)}{extraQuery}&page=1&pageSize={BalancePageSize}",
            cancellationToken);
        if (!reply.IsSuccess) return ([], false);
        return (reply.Read<BalancePageDto>()?.Rows ?? [], true);
    }

    public static async Task<(IReadOnlyList<SourceOption> Options, bool Available)> SourcesAsync(
        InventoryGatewayClient gateway, string skuId, string skuLevel, CancellationToken cancellationToken)
    {
        var (rows, available) = await BalanceRowsAsync(gateway, skuId, skuLevel, cancellationToken);
        if (!available) return ([], false);

        var withStock = rows.Where(r => TryDecimal(r.OnHand, out var onHand) && onHand > 0m
                                        && !string.IsNullOrWhiteSpace(r.WarehouseId) && !string.IsNullOrWhiteSpace(r.LocationId)
                                        && r.StockStatus is not null).ToList();
        var labels = await MasterLabelResolver.ResolveAsync(gateway, [], [], withStock.SelectMany(r => new[] { r.WarehouseId, r.LocationId }), cancellationToken);
        return (withStock.Select(r =>
        {
            var key = new MovementRules.SourceRow(r.WarehouseId!, r.LocationId!, r.StockStatus!, string.IsNullOrWhiteSpace(r.LotId) ? null : r.LotId).Key;
            var place = $"{labels.Location(r.WarehouseId) ?? r.WarehouseId} / {labels.Location(r.LocationId) ?? r.LocationId}";
            var text = string.Join(" · ", new[] { r.LotId, place }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return new SourceOption(key, text, r.WarehouseId!, r.LocationId!, r.LotId, r.StockStatus!, r.OnHand!, r.BaseUomId);
        }).ToList(), true);
    }

    /// <summary>W-UI-02: the lots this SKU already has in the ledger — offered first so a known lot is chosen, not retyped.</summary>
    public static async Task<(IReadOnlyList<string> Lots, bool Available)> KnownLotsAsync(
        InventoryGatewayClient gateway, string skuId, string skuLevel, CancellationToken cancellationToken)
    {
        var (rows, available) = await BalanceRowsAsync(gateway, skuId, skuLevel, cancellationToken);
        return (rows.Select(r => r.LotId).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!)
            .Distinct(StringComparer.Ordinal).OrderBy(l => l, StringComparer.Ordinal).ToList(), available);
    }

    /// <summary>
    /// Before → after of each side the movement touches, in BASE units (pack §12 "Stock effect"). Informational: it
    /// never decides sufficiency — a negative "after" is shown as such and the service still answers.
    /// </summary>
    public static async Task<IReadOnlyList<EffectSide>> EffectAsync(
        InventoryGatewayClient gateway, string skuId, string skuLevel, string baseQuantity,
        MovementRules.SourceRow? source, (string WarehouseId, string LocationId, string Status, string? Lot)? target,
        CancellationToken cancellationToken)
    {
        if (!TryDecimal(baseQuantity, out var moved)) return [];
        var sides = new List<EffectSide>();
        var ids = new List<string?>();
        if (source is not null) ids.AddRange([source.WarehouseId, source.LocationId]);
        if (target is not null) ids.AddRange([target.Value.WarehouseId, target.Value.LocationId]);
        var labels = await MasterLabelResolver.ResolveAsync(gateway, [], [], ids, cancellationToken);

        if (source is not null)
        {
            var before = await OnHandAsync(gateway, skuId, skuLevel, source.WarehouseId, source.LocationId, source.StockStatus, source.LotId, cancellationToken);
            if (before is not null)
            {
                var after = before.Value - moved;
                sides.Add(new EffectSide("source", Place(labels, source.WarehouseId, source.LocationId), Wire(before.Value), Wire(after), after < 0m));
            }
        }

        if (target is not null)
        {
            var (warehouse, location, status, lot) = target.Value;
            var before = await OnHandAsync(gateway, skuId, skuLevel, warehouse, location, status, lot, cancellationToken);
            if (before is not null)
                sides.Add(new EffectSide("target", Place(labels, warehouse, location), Wire(before.Value), Wire(before.Value + moved), false));
        }

        return sides;
    }

    private static async Task<decimal?> OnHandAsync(InventoryGatewayClient gateway, string skuId, string skuLevel,
        string warehouseId, string locationId, string status, string? lot, CancellationToken cancellationToken)
    {
        var query = $"&warehouseId={Uri.EscapeDataString(warehouseId)}&locationId={Uri.EscapeDataString(locationId)}&stockStatus={Uri.EscapeDataString(status)}"
                    + (lot is null ? string.Empty : $"&lotId={Uri.EscapeDataString(lot)}");
        var (rows, available) = await BalanceRowsAsync(gateway, skuId, skuLevel, cancellationToken, query);
        if (!available) return null;
        // /balance without lotId answers every lot: the row of THIS dimension tuple is the one whose lot matches.
        var row = rows.FirstOrDefault(r => string.Equals(string.IsNullOrWhiteSpace(r.LotId) ? null : r.LotId, lot, StringComparison.Ordinal));
        return row is not null && TryDecimal(row.OnHand, out var onHand) ? onHand : 0m;
    }

    private static string Place(MasterLabels labels, string warehouseId, string locationId) =>
        $"{labels.Location(warehouseId) ?? warehouseId} / {labels.Location(locationId) ?? locationId}";

    public static string Wire(decimal value)
    {
        var text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    // Trailing zeros are not precision: 24.000 has scale 0 once normalised.
    private static int Scale(decimal value) => (decimal.GetBits(value / 1.0000000000000000000000000000m)[3] >> 16) & 0x7F;

    public static bool TryDecimal(string? value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            }
            : null;

    /// <summary>A boolean flag, or null when it is absent, null or not a boolean (unknown — never false).</summary>
    private static bool? Flag(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
