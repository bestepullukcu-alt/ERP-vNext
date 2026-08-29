using System.Globalization;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>
/// Everything the FU08 validation pass needs, loaded once: the target model, its current nodes / rules / account
/// assignments, the accounts it may reference, and the published MOD-0048 vocabularies.
///
/// <para>The context is <b>read-only</b>. It is what makes "import is a transport lane, not a second business-rule
/// engine" enforceable — the validators compare the file against the same state the on-screen commands see, and the
/// apply step then goes through those commands' own guards.</para>
/// </summary>
public sealed class TerritoryImportContext
{
    public required TerritoryModel Model { get; init; }
    public required IReadOnlyList<TerritoryNode> Nodes { get; init; }
    public required IReadOnlyList<TerritoryAssignmentRule> Rules { get; init; }
    public required IReadOnlyList<AccountTerritoryAssignment> Assignments { get; init; }
    public required IReadOnlyList<TerritoryAccountSnapshot> Accounts { get; init; }
    public required IReadOnlyList<TerritoryModel> OtherActiveModels { get; init; }

    /// <summary>Published value codes per MOD-0048 set. A set absent here is NOT published → fail closed.</summary>
    public required IReadOnlyDictionary<string, HashSet<string>> PublishedValues { get; init; }

    /// <summary>territory-level rank metadata (child rank must be greater than its parent's).</summary>
    public required IReadOnlyDictionary<string, int> LevelRanks { get; init; }

    public bool IsModelDraft => Is(Model.Status, "draft");
    public bool IsModelActive => Is(Model.Status, "active");

    public TerritoryNode? NodeByCode(string? code)
        => code is null ? null : Nodes.FirstOrDefault(n => Is(n.TerritoryCode, code));

    public TerritoryNode? NodeById(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    public TerritoryAssignmentRule? RuleByCode(string? code)
        => code is null ? null : Rules.FirstOrDefault(r => Is(r.RuleCode, code));

    public TerritoryAccountSnapshot? AccountByCode(string? code)
        => code is null ? null : Accounts.FirstOrDefault(a => Is(a.AccountCode, code));

    public TerritoryAccountSnapshot? AccountById(Guid id) => Accounts.FirstOrDefault(a => a.AccountId == id);

    public bool IsPublished(string setCode, string? value)
        => value is not null
           && PublishedValues.TryGetValue(setCode, out var values)
           && values.Contains(value.Trim());

    public bool SetPublished(string setCode)
        => PublishedValues.TryGetValue(setCode, out var values) && values.Count > 0;

    public static bool Is(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Parsing helpers shared by every FU08 validator. All of them are total: they never throw on bad input,
/// they report it.</summary>
public static class TerritoryImportValues
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-dd HH:mm:ss",
        "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy"
    ];

    /// <summary>Splits a multi-value cell ("alpha; beta"). Empty entries are dropped and values are trimmed.</summary>
    public static List<string> SplitList(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => v.Length > 0)
                .ToList();

    public static bool TryDate(string? raw, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim();
        if (DateTimeOffset.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
        {
            return true;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }

    public static bool TryGuid(string? raw, out Guid value)
        => Guid.TryParse(raw?.Trim(), out value);

    public static bool TryInt(string? raw, out int value)
        => int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool? TryBool(string? raw)
    {
        var text = raw?.Trim().ToLowerInvariant();
        return text switch
        {
            null or "" => null,
            "true" or "1" or "yes" or "y" or "evet" => true,
            "false" or "0" or "no" or "n" or "hayır" or "hayir" => false,
            _ => null
        };
    }

    public static string Iso(DateTimeOffset value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? IsoOrNull(DateTimeOffset? value) => value is { } v ? Iso(v) : null;

    /// <summary>Window containment by calendar DATE, not instant — the same convention FU05 apply uses, so a
    /// same-date boundary written in a different offset is not falsely rejected.</summary>
    public static bool Contains(DateTimeOffset outerFrom, DateTimeOffset? outerTo, DateTimeOffset from, DateTimeOffset? to)
    {
        if (from.Date < outerFrom.Date) return false;
        if (outerTo is not { } end) return true;
        return to is { } inner ? inner.Date <= end.Date : false;
    }

    public static bool WindowsOverlap(DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo)
        => aFrom <= (bTo ?? DateTimeOffset.MaxValue) && bFrom <= (aTo ?? DateTimeOffset.MaxValue);
}
