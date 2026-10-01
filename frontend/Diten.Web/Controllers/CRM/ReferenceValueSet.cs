using System.Text.Json;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CL-FE-2 logic shared with WP-KP-UI-1 — the published values of a MOD-0048 BRD set as the Web reads them (active,
/// not deprecated, BRD order) and the country axis built from <c>COUNTRY_CODES</c> + <c>country-content-languages</c>
/// with ICU display names (<see cref="ClaimDisplayNames"/>). Used by the claim screens (<c>ClaimsController</c>
/// v2 lookups/countries) and the knowledge path studio (<c>KnowledgePathsController</c> lookups/countries).
/// </summary>
internal static class ReferenceValueSet
{
    internal sealed record Value(string Code, string? Name, IReadOnlyDictionary<string, string> Attributes);

    /// <summary>The values of a published-values <c>data</c> element (object with <c>items</c>, or an array); null when
    /// it is neither (unavailable).</summary>
    public static IReadOnlyList<Value>? Parse(JsonElement? data)
    {
        JsonElement items;
        if (data is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("items", out var it)
            && it.ValueKind == JsonValueKind.Array) items = it;
        else if (data is { ValueKind: JsonValueKind.Array } arr) items = arr;
        else return null;

        var values = new List<(Value Value, int Order, int Index)>();
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object) continue;
            if ((item.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False)
                || (item.TryGetProperty("isDeprecated", out var dep) && dep.ValueKind == JsonValueKind.True)) continue;
            if (Str(item, "valueCode", "value_code", "code") is not { } code) continue;

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (item.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in attrs.EnumerateObject())
                    attributes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
            }

            var order = item.TryGetProperty("sortOrder", out var so) && so.TryGetInt32(out var n) ? n : int.MaxValue;
            values.Add((new Value(code, Str(item, "displayName", "display_name", "name"), attributes), order, index));
        }

        return values.OrderBy(v => v.Order).ThenBy(v => v.Index).Select(v => v.Value).ToList();
    }

    /// <summary>One country of the axis: ICU name (UI culture) + native name, its content languages (first = default) as
    /// plain codes and as named details. A code ICU does not know falls back to the BRD display name, then the code.</summary>
    internal sealed record Country(
        string Code, string Name, string NativeName, IReadOnlyList<string> Languages, IReadOnlyList<Language> LanguageDetails);

    internal sealed record Language(string Code, string Name, string NativeName);

    public static IReadOnlyList<Country> Countries(IReadOnlyList<Value> countries, IReadOnlyList<Value>? languages)
    {
        var byCountry = (languages ?? [])
            .ToDictionary(v => v.Code.ToUpperInvariant(), v => v, StringComparer.Ordinal);
        return countries.Select(c =>
        {
            var code = c.Code.ToUpperInvariant();
            var codes = byCountry.TryGetValue(code, out var l) && l.Attributes.TryGetValue("Languages", out var list)
                ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();
            return new Country(
                code,
                ClaimDisplayNames.CountryName(code) ?? c.Name ?? code,
                ClaimDisplayNames.CountryNativeName(code, codes.FirstOrDefault()) ?? c.Name ?? code,
                codes,
                codes.Select(LanguageOf).ToList());
        }).ToList();
    }

    public static Language LanguageOf(string code)
        => new(code, ClaimDisplayNames.LanguageName(code) ?? code, ClaimDisplayNames.LanguageNativeName(code) ?? code);

    private static string? Str(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
                return p.GetString();
        }

        return null;
    }
}
