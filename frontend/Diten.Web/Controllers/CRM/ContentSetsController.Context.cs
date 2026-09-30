using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-SB-1R (bridge-decision §7) — the content set context on the Web side. The retired ContentScope picker is replaced by
/// a country + language choice read from BRD: the country axis is the GLOBAL <c>COUNTRY_CODES</c> set, each country's
/// languages the GLOBAL <c>country-content-languages</c> set (attribute <c>Languages</c>). Names come from ICU in the
/// request's UI culture (<see cref="ClaimDisplayNames"/>, the FE-2 helper) — never a compiled list. CRM validates the
/// choice again on write (country_invalid / language_not_in_country); this lookup only feeds the pickers.
/// </summary>
public sealed partial class ContentSetsController
{
    private const string ReferenceDataBase = "/api/v1/reference-data/sets";

    /// <summary>The CRM context error codes shown in the user's language (resx key <c>ContextError_{code}</c>).</summary>
    internal static readonly IReadOnlyList<string> ContextErrorCodes =
    [
        "country_invalid", "language_not_in_country", "reference_set_unavailable", "component_language_mismatch",
        "context_locked", "component_language_mixed"
    ];

    /// <summary>Countries (BRD order) with their content languages: <c>{ data: [{ code, name, nativeName, languages:
    /// [{ code, name, nativeName }] }] }</c>. COUNTRY_CODES unreadable ⇒ 503 <c>reference_set_unavailable</c>; the
    /// languages set unreadable only empties <c>languages</c>.</summary>
    [HttpGet("api/countries")]
    public async Task<IActionResult> Countries(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;

        var countries = await ReadGlobalSetAsync("COUNTRY_CODES", ct);
        if (countries is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { errors = new[] { "reference_set_unavailable", "COUNTRY_CODES is not available." } });

        var languages = (await ReadGlobalSetAsync("country-content-languages", ct) ?? [])
            .GroupBy(v => v.Code.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var data = countries.Select(c =>
        {
            var code = c.Code.ToUpperInvariant();
            var codes = languages.TryGetValue(code, out var row) && row.Languages is { } list
                ? list.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim().ToLowerInvariant()).Distinct().ToArray()
                : Array.Empty<string>();
            return new
            {
                code,
                name = ClaimDisplayNames.CountryName(code) ?? c.Name ?? code,
                nativeName = ClaimDisplayNames.CountryNativeName(code, codes.FirstOrDefault()) ?? c.Name ?? code,
                languages = codes.Select(lang => new
                {
                    code = lang,
                    name = ClaimDisplayNames.LanguageName(lang) ?? lang,
                    nativeName = ClaimDisplayNames.LanguageNativeName(lang) ?? lang
                }).ToList()
            };
        }).ToList();
        return Ok(new { data });
    }

    /// <summary>Turns CRM <c>[code, message]</c> pairs of the context codes into the localized text; other errors pass
    /// through unchanged.</summary>
    private List<string> LocalizeErrors(IReadOnlyList<string> errors)
    {
        var result = new List<string>();
        for (var i = 0; i < errors.Count; i++)
        {
            var code = errors[i]?.Trim() ?? string.Empty;
            if (ContextErrorCodes.Contains(code, StringComparer.Ordinal))
            {
                result.Add(_localizer["ContextError_" + code].Value);
                if (i + 1 < errors.Count && !ContextErrorCodes.Contains(errors[i + 1]?.Trim() ?? string.Empty, StringComparer.Ordinal))
                {
                    i++; // the English message of the pair
                }

                continue;
            }

            result.Add(errors[i]);
        }

        return result;
    }

    private sealed record GlobalValue(string Code, string? Name, string? Languages);

    /// <summary>Active, non-deprecated values of a GLOBAL BRD set (read WITHOUT scope_key), in BRD order. Null =
    /// unavailable.</summary>
    private async Task<IReadOnlyList<GlobalValue>?> ReadGlobalSetAsync(string setCode, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get,
            $"{ReferenceDataBase}/{Uri.EscapeDataString(setCode)}/published-values", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            JsonElement items;
            if (data.ValueKind == JsonValueKind.Array) items = data;
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var it)
                     && it.ValueKind == JsonValueKind.Array) items = it;
            else return null;

            var values = new List<(GlobalValue Value, int Order, int Index)>();
            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                index++;
                if (item.ValueKind != JsonValueKind.Object) continue;
                if ((item.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False)
                    || (item.TryGetProperty("isDeprecated", out var deprecated) && deprecated.ValueKind == JsonValueKind.True)) continue;
                if (Text(item, "valueCode", "value_code", "code") is not { } code) continue;
                var languages = item.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object
                    ? Text(attributes, "Languages", "languages")
                    : null;
                var order = item.TryGetProperty("sortOrder", out var so) && so.TryGetInt32(out var n) ? n : int.MaxValue;
                values.Add((new GlobalValue(code, Text(item, "displayName", "display_name", "name"), languages), order, index));
            }

            return values.OrderBy(v => v.Order).ThenBy(v => v.Index).Select(v => v.Value).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Reference set {SetCode} parse failed.", setCode);
            return null;
        }
    }

    private static string? Text(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(p.GetString()))
                return p.GetString();
        }

        return null;
    }
}
