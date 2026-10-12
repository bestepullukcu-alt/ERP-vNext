using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Diten.Web.Models.Traceability;

/// <summary>
/// MOD-0174 P3 list adapters (pack §11.3): the repo's server-mode list wire (<c>start, length, search, orderBy, orderDir,
/// draw, &lt;filters&gt;</c>) → TRACE v1.1 <c>listLots</c> / <c>listSerials</c> (<c>page, pageSize ≤ 200, q, …</c>). Pure
/// translation, validated before anything reaches the Gateway; a value outside the contract's set is a 400, never sent.
/// </summary>
public static class TraceListAdapter
{
    public const string InvalidQuery = "TRACE_LIST_INVALID";
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 100;

    public static readonly IReadOnlyList<string> LotStatuses = ["Draft", "Released", "Blocked", "Expired"];
    public static readonly IReadOnlyList<string> SerialStatuses = ["Active", "Consumed", "Blocked"];

    /// <summary>listLots sort fields (contract): the list's column name → the operation's sort value.</summary>
    private static readonly IReadOnlyDictionary<string, string> LotSort = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["lotNumber"] = "lotNumber",
        ["expiryDate"] = "expiryDate",
        ["createdAt"] = "createdAt"
    };

    public sealed record Translation(string? Query, string? ErrorCode)
    {
        public bool IsValid => ErrorCode is null;
    }

    /// <summary>Lot Gezgini → <c>GET /api/trace/lots?…&amp;includeStock=true</c> (K-A: "Eldeki Stok" from <c>stockSummary</c>).</summary>
    public static Translation LotsQuery(IQueryCollection query)
    {
        var parts = new List<string>();
        if (!Paging(query, parts)) return Invalid();
        if (!Search(query, "q", parts)) return Invalid();

        var item = Single(query["itemId"]);
        if (item is not null)
        {
            if (!Guid.TryParse(item, out var itemId)) return Invalid();
            parts.Add("itemId=" + itemId.ToString("D"));
        }

        var status = Single(query["status"]);
        if (status is not null)
        {
            if (!LotStatuses.Contains(status, StringComparer.Ordinal)) return Invalid();
            parts.Add("status=" + status);
        }

        var before = Single(query["expiryBefore"]);
        if (before is not null)
        {
            if (!DateOnly.TryParseExact(before, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return Invalid();
            parts.Add("expiryBefore=" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        var warn = Single(query["warn"]);
        if (warn is not null)
        {
            if (warn is not ("true" or "false")) return Invalid();
            if (warn == "true") parts.Add("warn=true");
        }

        var orderBy = Single(query["orderBy"]);
        if (orderBy is not null)
        {
            if (!LotSort.TryGetValue(orderBy, out var sort)) return Invalid();
            parts.Add("sort=" + (Single(query["orderDir"]) == "desc" ? "-" : string.Empty) + sort);
        }

        parts.Add("includeStock=true");
        return new Translation("?" + string.Join("&", parts), null);
    }

    /// <summary>Seri Gezgini → <c>GET /api/trace/serials?…</c> (15 rows per page by default on the screen).</summary>
    public static Translation SerialsQuery(IQueryCollection query)
    {
        var parts = new List<string>();
        if (!Paging(query, parts)) return Invalid();
        if (!Search(query, "q", parts)) return Invalid();

        var sku = Single(query["sku"]);
        if (sku is not null)
        {
            if (!LotFormRules.TryParseSku(sku, out var level, out var skuId)) return Invalid();
            parts.Add("skuId=" + skuId);
            parts.Add("skuLevel=" + level);
        }

        var lot = Single(query["lotId"]);
        if (lot is not null)
        {
            if (!Guid.TryParse(lot, out var lotId)) return Invalid();
            parts.Add("lotId=" + lotId.ToString("D"));
        }

        var status = Single(query["status"]);
        if (status is not null)
        {
            if (!SerialStatuses.Contains(status, StringComparer.Ordinal)) return Invalid();
            parts.Add("status=" + status);
        }

        return new Translation("?" + string.Join("&", parts), null);
    }

    private static bool Paging(IQueryCollection query, List<string> parts)
    {
        var start = ReadInt(query["start"], 0);
        var length = ReadInt(query["length"], 10);
        if (start is null || length is null || start < 0 || length < 1 || length > MaxPageSize || start.Value % length.Value != 0) return false;
        parts.Add("page=" + (start.Value / length.Value + 1).ToString(CultureInfo.InvariantCulture));
        parts.Add("pageSize=" + length.Value.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    private static bool Search(IQueryCollection query, string target, List<string> parts)
    {
        var search = Single(query["search"]);
        if (search is null) return true;
        if (search.Length > MaxSearchLength) return false;
        parts.Add(target + "=" + Uri.EscapeDataString(search));
        return true;
    }

    private static string? Single(StringValues values)
    {
        var value = values.Count == 1 ? values[0]?.Trim() : null;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int? ReadInt(StringValues values, int fallback)
    {
        if (values.Count == 0) return fallback;
        return int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static Translation Invalid() => new(null, InvalidQuery);
}
