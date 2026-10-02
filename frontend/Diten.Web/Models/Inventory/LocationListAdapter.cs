using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// MOD-0173-FU02 L-2 list adapter (pack §11, LOCATION v1.1 <c>searchLocations</c>). The repo's server-mode list sends
/// <c>start, length, search, orderBy, orderDir, draw, &lt;filters&gt;</c> and reads <c>{ data: { items, total,
/// filteredTotal } }</c>; <c>searchLocations</c> takes <c>page, pageSize ≤ 200, search, type*, status*, isStockable,
/// parentId, orderBy (code|name|type|status), orderDir</c> and answers <c>{ items, total, page, pageSize }</c>. Unlike
/// FU01 (BG-04) search and sort ARE supported here. Pure translation, validated before anything reaches the Gateway.
/// <list type="bullet">
/// <item><c>page = start / length + 1</c>; <c>length</c> 1…200; <c>start % length ≠ 0</c> → 400.</item>
/// <item><c>orderBy</c> outside the contract's four columns → 400; <c>search</c> ≤ 100 characters.</item>
/// <item>Filters are the contract enums (never a hardcoded lookup list of data); a value outside them → 400.</item>
/// </list>
/// </summary>
public static class LocationListAdapter
{
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 100;
    public const string InvalidQuery = "LOCATION_LIST_INVALID";

    /// <summary>LOCATION v1 <c>LocationType</c>, level order (K-01).</summary>
    public static readonly IReadOnlyList<string> Types = ["Site", "Warehouse", "StorageLocation", "Zone", "Bin"];

    public static readonly IReadOnlyList<string> Statuses = ["Active", "Inactive", "Archived"];

    public static readonly IReadOnlyList<string> OperatingModes = ["internal", "external-integrated"];

    private static readonly HashSet<string> OrderColumns = new(StringComparer.Ordinal) { "code", "name", "type", "status" };

    /// <summary>K-01 parent matrix (pack §12) — what the form offers; the service stays the authority.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> AllowedParents = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Site"] = [],
        ["Warehouse"] = ["Site"],
        ["StorageLocation"] = ["Warehouse"],
        ["Zone"] = ["StorageLocation"],
        ["Bin"] = ["Zone", "StorageLocation"]
    };

    public sealed record Translation(string? Query, int Draw, string? ErrorCode, string? ErrorDetail)
    {
        public bool IsValid => ErrorCode is null;
    }

    /// <summary>L-2 → <c>GET /api/locations/search?…</c>.</summary>
    public static Translation SearchQuery(IQueryCollection query)
    {
        var start = ReadInt(query["start"], 0);
        var length = ReadInt(query["length"], 50);
        var draw = ReadInt(query["draw"], 0);
        if (start is null || length is null || draw is null || start < 0 || length < 1 || length > MaxPageSize)
            return Invalid("start/length");
        if (start.Value % length.Value != 0)
            return Invalid("start");

        var parts = new List<string>
        {
            "page=" + (start.Value / length.Value + 1).ToString(CultureInfo.InvariantCulture),
            "pageSize=" + length.Value.ToString(CultureInfo.InvariantCulture)
        };

        var search = Single(query["search"]);
        if (search is not null)
        {
            if (search.Length > MaxSearchLength) return Invalid("search");
            parts.Add("search=" + Uri.EscapeDataString(search));
        }

        if (!TryAddEnumList(parts, "type", query["type"], Types)) return Invalid("type");
        if (!TryAddEnumList(parts, "status", query["status"], Statuses)) return Invalid("status");

        var stockable = Single(query["isStockable"]);
        if (stockable is not null)
        {
            if (stockable is not ("true" or "false")) return Invalid("isStockable");
            parts.Add("isStockable=" + stockable);
        }

        var parent = Single(query["parentId"]);
        if (parent is not null)
        {
            if (!Guid.TryParse(parent, out var parentId)) return Invalid("parentId");
            parts.Add("parentId=" + parentId.ToString("D"));
        }

        var orderBy = Single(query["orderBy"]);
        var orderDir = Single(query["orderDir"]);
        if (orderBy is not null)
        {
            if (!OrderColumns.Contains(orderBy)) return Invalid("orderBy");
            if (orderDir is not null && orderDir is not ("asc" or "desc")) return Invalid("orderDir");
            parts.Add("orderBy=" + orderBy);
            parts.Add("orderDir=" + (orderDir ?? "asc"));
        }
        else if (orderDir is not null)
        {
            return Invalid("orderDir");
        }

        return new Translation("?" + string.Join("&", parts), draw.Value, null, null);
    }

    /// <summary>
    /// The tree's children of one parent (L-1, lazy): <c>searchLocations?parentId=…</c>, or the company's Sites for the
    /// root. Archived nodes only when the reader asks for them ("show archived").
    /// </summary>
    public static string ChildrenQuery(Guid? parentId, bool includeArchived, int page, int pageSize)
    {
        var parts = new List<string>
        {
            "page=" + page.ToString(CultureInfo.InvariantCulture),
            "pageSize=" + pageSize.ToString(CultureInfo.InvariantCulture),
            parentId is null ? "type=Site" : "parentId=" + parentId.Value.ToString("D")
        };
        if (!includeArchived)
        {
            parts.Add("status=Active");
            parts.Add("status=Inactive");
        }

        parts.Add("orderBy=code");
        parts.Add("orderDir=asc");
        return "?" + string.Join("&", parts);
    }

    /// <summary>
    /// The parent picker (form "Parent", L-2 "Parent" filter): a code/name search among the given types, Active only
    /// when the choice is for a write (K-06: a new child or a move needs an Active parent).
    /// </summary>
    public static string? ParentsQuery(string? search, IEnumerable<string> types, bool activeOnly)
    {
        var parts = new List<string> { "page=1", "pageSize=20", "orderBy=code", "orderDir=asc" };
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is not null)
        {
            if (term.Length > MaxSearchLength) return null;
            parts.Add("search=" + Uri.EscapeDataString(term));
        }

        foreach (var type in types)
        {
            if (!Types.Contains(type, StringComparer.Ordinal)) return null;
            parts.Add("type=" + type);
        }

        if (activeOnly) parts.Add("status=Active");
        return "?" + string.Join("&", parts);
    }

    /// <summary>A search row as the list draws it; the parent's path is filled in by the controller (one call per parent).</summary>
    public static LocationRowViewModel Row(LocationRefDto location, string? parentPath) => new()
    {
        Id = location.LocationId,
        Code = location.Code,
        Name = location.Name,
        Type = location.Type,
        ParentId = location.ParentId,
        ParentPath = parentPath,
        Status = location.Status,
        IsStockable = location.IsStockable,
        OperatingMode = location.OperatingMode
    };

    /// <summary><c>getLocationPath</c> → "TR / WH-01 / SO" (codes, root Site first).</summary>
    public static string? PathText(LocationPathDto? path) =>
        path?.Path is { Count: > 0 } steps
            ? string.Join(" / ", steps.Select(s => s.Code ?? s.Id))
            : null;

    private static Translation Invalid(string field) => new(null, 0, InvalidQuery, field);

    private static bool TryAddEnumList(List<string> parts, string name, StringValues values, IReadOnlyList<string> allowed)
    {
        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var value = raw.Trim();
            if (!allowed.Contains(value, StringComparer.Ordinal)) return false;
            parts.Add(name + "=" + value);
        }

        return true;
    }

    private static string? Single(StringValues values)
    {
        var value = values.Count > 0 ? values[^1] : null;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadInt(StringValues values, int fallback)
    {
        var raw = Single(values);
        if (raw is null) return fallback;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
