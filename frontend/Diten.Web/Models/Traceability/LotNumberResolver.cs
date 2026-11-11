using System.Text.Json;

namespace Diten.Web.Models.Traceability;

/// <summary>
/// WP-MVP1-FIX-LIVE1 — the lot NUMBER of the registered lot ids on one list page (the 2026-10-14 live finding: Stok Defteri showed the
/// lot GUID). TRACE has no "lots by ids" read, so the page is resolved per SKU with the existing listLots filter
/// (<c>GET /api/trace/lots?skuId&amp;skuLevel&amp;page=1&amp;pageSize=200</c> — the Lots / LotMigration pattern): ONE call per distinct SKU that
/// has a registered lot on the page, never one per lot. Rows are TRACE <c>LotRef { lotId, lotNumber }</c> (trace.openapi.yaml). A lot
/// that is not in its SKU's first 200 lots, a failed call or a caller without <c>trace.lots.read</c> leaves the lot unresolved — the page
/// shows the id with an "unresolved" hint, never an error.
/// </summary>
public static class LotNumberResolver
{
    public const string ReadKey = "trace.lots.read";
    public const int PageSize = 200;

    public sealed record Row(string? LotId, string? SkuId, string? SkuLevel);

    /// <returns>lotId → lotNumber for every lot that could be read (ordinal ids).</returns>
    public static async Task<IReadOnlyDictionary<string, string>> ResolveAsync(TraceGatewayClient? trace, IEnumerable<Row> rows, CancellationToken cancellationToken)
    {
        var numbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (trace is null) return numbers;

        var skus = rows
            .Where(r => Guid.TryParse(r.LotId, out _) && Guid.TryParse(r.SkuId, out _) && !string.IsNullOrWhiteSpace(r.SkuLevel))
            .Select(r => (Sku: r.SkuId!, Level: r.SkuLevel!))
            .Distinct()
            .ToList();
        var replies = await Task.WhenAll(skus.Select(s => trace.GetAsync(
            $"/api/trace/lots?skuId={Uri.EscapeDataString(s.Sku)}&skuLevel={Uri.EscapeDataString(s.Level)}&page=1&pageSize={PageSize}", cancellationToken)));

        foreach (var reply in replies.Where(r => r.IsSuccess))
        {
            try
            {
                using var doc = JsonDocument.Parse(reply.Body);
                if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) continue;
                foreach (var lot in items.EnumerateArray())
                {
                    if (lot.ValueKind == JsonValueKind.Object
                        && lot.TryGetProperty("lotId", out var id) && id.ValueKind == JsonValueKind.String
                        && lot.TryGetProperty("lotNumber", out var number) && number.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(number.GetString()))
                    {
                        numbers[id.GetString()!] = number.GetString()!;
                    }
                }
            }
            catch (JsonException)
            {
                // an unreadable answer resolves nothing
            }
        }

        return numbers;
    }

    /// <summary>A REGISTERED lot (a TRACE id) whose number could not be read; a typed lot text (pre-MOD-0174) is not "unresolved".</summary>
    public static bool Unresolved(string? lotId, IReadOnlyDictionary<string, string> numbers) =>
        Guid.TryParse(lotId, out _) && !numbers.ContainsKey(lotId!);
}
