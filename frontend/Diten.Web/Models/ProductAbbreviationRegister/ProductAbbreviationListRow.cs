using System.Text.Json;

namespace Diten.Web.Models.ProductAbbreviationRegister;

/// <summary>
/// WP-MDM-ABB-LIST-01 — one row of the register list as the page's columns name it (camelCase on the wire). Read from
/// MDM's one-product entry list by name, case-insensitively, so the list does not depend on MDM's casing.
/// </summary>
public sealed record ProductAbbreviationListRow(
    Guid Id,
    Guid GlobalProductId,
    string Abbreviation,
    JsonElement LifecycleStatus,
    int Version,
    bool RetirementPending,
    bool CorrectionPending,
    bool RequestedByCurrentUser,
    bool RetirementRequestedByCurrentUser)
{
    /// <summary>MDM's lifecycle order, for a status that arrives as its name.</summary>
    private static readonly string[] LifecycleOrder = ["REQUESTED", "ACTIVE", "REJECTED", "CANCELLED", "RETIRED"];

    /// <summary>The lifecycle's NAME whether MDM sent the name or its number (MDM's enum order); null when unreadable.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? LifecycleName => LifecycleStatus.ValueKind switch
    {
        JsonValueKind.String when Array.IndexOf(LifecycleOrder, LifecycleStatus.GetString()) >= 0 => LifecycleStatus.GetString(),
        JsonValueKind.Number when LifecycleStatus.TryGetInt32(out var number) && number >= 0 && number < LifecycleOrder.Length
            => LifecycleOrder[number],
        _ => null
    };

    /// <summary>LIST-FIX1 item 4 — an active or still-requested code: the product takes no new request.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsOpen => LifecycleName is "ACTIVE" or "REQUESTED";

    /// <summary>The entries of MDM's envelope (<c>{ data: [ … ] }</c>); false when the answer is not that shape.</summary>
    public static bool TryReadEntries(string body, out List<ProductAbbreviationListRow> rows)
    {
        rows = [];
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryGet(document.RootElement, "data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !TryGet(item, "id", out var id) || !id.TryGetGuid(out var entryId)
                    || !TryGet(item, "abbreviation", out var abbreviation) || abbreviation.ValueKind != JsonValueKind.String
                    // LIST-FIX2 item 6 — a row without a readable lifecycle is not an entry: the read is refused (502)
                    // instead of an undefined JsonElement failing the serializer later (an unhandled 500).
                    || !TryGet(item, "lifecycleStatus", out var lifecycle)
                    || lifecycle.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                {
                    return false;
                }

                rows.Add(new ProductAbbreviationListRow(
                    entryId,
                    TryGet(item, "globalProductId", out var product) && product.TryGetGuid(out var productId) ? productId : Guid.Empty,
                    abbreviation.GetString() ?? string.Empty,
                    lifecycle.Clone(),
                    TryGet(item, "version", out var version) && version.TryGetInt32(out var number) ? number : 0,
                    Flag(item, "retirementPending"),
                    Flag(item, "correctionPending"),
                    Flag(item, "requestedByCurrentUser"),
                    Flag(item, "retirementRequestedByCurrentUser")));
            }

            return true;
        }
        catch (JsonException)
        {
            rows = [];
            return false;
        }
    }

    /// <summary>The rows sorted by one whitelisted column; the abbreviation and the id break ties, so a page is stable.</summary>
    public static List<ProductAbbreviationListRow> Order(
        IReadOnlyCollection<ProductAbbreviationListRow> rows,
        string orderBy,
        bool descending)
    {
        IOrderedEnumerable<ProductAbbreviationListRow> sorted = orderBy switch
        {
            "lifecycleStatus" => descending
                ? rows.OrderByDescending(row => LifecycleRank(row.LifecycleStatus))
                : rows.OrderBy(row => LifecycleRank(row.LifecycleStatus)),
            "version" => descending ? rows.OrderByDescending(row => row.Version) : rows.OrderBy(row => row.Version),
            "retirementPending" => descending
                ? rows.OrderByDescending(row => row.RetirementPending)
                : rows.OrderBy(row => row.RetirementPending),
            _ => descending
                ? rows.OrderByDescending(row => row.Abbreviation, StringComparer.Ordinal)
                : rows.OrderBy(row => row.Abbreviation, StringComparer.Ordinal)
        };
        return sorted
            .ThenBy(row => row.Abbreviation, StringComparer.Ordinal)
            .ThenBy(row => row.Id)
            .ToList();
    }

    private static int LifecycleRank(JsonElement status) => status.ValueKind switch
    {
        JsonValueKind.Number when status.TryGetInt32(out var number) => number,
        JsonValueKind.String => Array.IndexOf(LifecycleOrder, status.GetString()) is var index and >= 0 ? index : int.MaxValue,
        _ => int.MaxValue
    };

    private static bool Flag(JsonElement item, string name)
        => TryGet(item, name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryGet(JsonElement item, string name, out JsonElement value)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
