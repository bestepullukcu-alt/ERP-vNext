using Diten.DevEnablementService.Domain.Repositories;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;

/// <summary>
/// THE `orderBy` WHITELIST. The wire names are the DataTables column `data` names of the Golden Compact list; nothing
/// else maps to a sort. A name outside this table is a 400 (the validator), never a silent fallback to `code` — a
/// fallback would make a typo on the page look like a working sort.
/// </summary>
public static class GoldenReferenceCompactListSort
{
    private static readonly IReadOnlyDictionary<string, GoldenReferenceCompactSortField> Columns =
        new Dictionary<string, GoldenReferenceCompactSortField>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = GoldenReferenceCompactSortField.Code,
            ["name"] = GoldenReferenceCompactSortField.Name,
            ["referenceType"] = GoldenReferenceCompactSortField.ReferenceType,
            ["category"] = GoldenReferenceCompactSortField.Category,
            ["owner"] = GoldenReferenceCompactSortField.Owner,
            ["version"] = GoldenReferenceCompactSortField.Version,
            ["priority"] = GoldenReferenceCompactSortField.Priority,
            ["isActive"] = GoldenReferenceCompactSortField.IsActive
        };

    public static IEnumerable<string> Names => Columns.Keys;

    public static bool TryResolve(string? orderBy, out GoldenReferenceCompactSortField field)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            field = GoldenReferenceCompactSortField.Code;
            return true;
        }

        return Columns.TryGetValue(orderBy.Trim(), out field);
    }

    public static bool IsDirection(string? orderDir) =>
        string.IsNullOrWhiteSpace(orderDir)
        || string.Equals(orderDir, "asc", StringComparison.OrdinalIgnoreCase)
        || string.Equals(orderDir, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>The status filter speaks the page's words; the entity stores a bool.</summary>
    public static bool TryStatus(string value, out bool isActive)
    {
        isActive = string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase);
        return isActive || string.Equals(value, "Passive", StringComparison.OrdinalIgnoreCase);
    }
}
