using System.Text.Json;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CL-FE-5 logic shared with WP-KP-UI-1 — reading the CRM claim coverage matrix (one call: the claim rows of a product,
/// each with its country cells) and deciding whether a claim (country version) is usable. CRM stays the authority; this
/// only drives warnings and pickers.
/// <list type="bullet">
/// <item>usable = <c>approved</c> or <c>review-required</c>, else <c>not_approved</c>;</item>
/// <item>a usable country version whose languages do not include the wanted language is <c>language_mismatch</c>
/// (only when the languages are known — an unknown language set is never guessed).</item>
/// </list>
/// Used by the knowledge content claim picker (<c>KnowledgeController.ClaimOptions</c>) and the knowledge path studio
/// claim search (<c>KnowledgePathsController</c> path-claims).
/// </summary>
internal static class ClaimCoverageOptions
{
    public const string NotApproved = "not_approved";
    public const string LanguageMismatch = "language_mismatch";
    public const string NoCountryVersion = "no_country_version";

    /// <summary>One country cell; <see cref="VersionId"/> is null for a closed / not-opened / not-applicable cell.</summary>
    internal sealed record Cell(string CountryCode, string? State, string? VersionId, string? Version);

    /// <summary>One coverage row (the current record of a claim code).</summary>
    internal sealed record Row(
        string ClaimId, string ClaimCode, string ClaimName, string Kind, string? CoreVersion, string? CoreStatus,
        IReadOnlyList<Cell> Cells);

    /// <summary>The rows of a coverage response body, or null when the body is not the coverage shape.</summary>
    public static IReadOnlyList<Row>? ReadRows(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<Row>();
        foreach (var row in rows.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
        {
            var claimId = Str(row, "claimId");
            var claimCode = Str(row, "claimCode");
            if (claimId is null || claimCode is null) continue;

            var cells = new List<Cell>();
            if (row.TryGetProperty("cells", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var cell in list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
                {
                    if (Str(cell, "countryCode")?.ToUpperInvariant() is not { } country) continue;
                    cells.Add(new Cell(country, Str(cell, "state"), Str(cell, "versionId"), Str(cell, "version")));
                }
            }

            result.Add(new Row(claimId, claimCode, Str(row, "claimName") ?? claimCode, Str(row, "kind") ?? "core",
                Str(row, "coreVersion"), Str(row, "coreStatus"), cells));
        }

        return result;
    }

    public static bool IsUsableStatus(string? status) =>
        string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "review-required", StringComparison.OrdinalIgnoreCase);

    /// <summary>The reason a country version cannot be used for <paramref name="language"/>, or null when it can.
    /// <paramref name="languages"/> null = unknown (the language rule is then skipped, never guessed).</summary>
    public static string? CountryReason(string? status, string? language, IReadOnlyCollection<string>? languages)
        => !IsUsableStatus(status) ? NotApproved
            : !string.IsNullOrWhiteSpace(language) && languages is not null
              && !languages.Contains(language.Trim(), StringComparer.OrdinalIgnoreCase) ? LanguageMismatch
            : null;

    private static string? Str(JsonElement element, string name)
        => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(p.GetString())
            ? p.GetString()
            : null;
}
