using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CL-FE-5 — knowledge content ↔ claim binding (claims v2, D4). The picker reads ONE CRM coverage call (the claim
/// rows of the content's product, each with its country cells) plus the global BRD <c>country-content-languages</c>
/// set; nothing is written here. CRM stays the authority: the publish gate (<c>claim_not_approved</c> /
/// <c>claim_language_mismatch</c>) is not repeated on the client — <c>usable</c>/<c>reason</c> only drive a warning.
/// <para>Language rule: CRM checks the content language against the country version's TEXT languages. A version can
/// only reach approved / review-required with texts for exactly the country's content languages (submit refuses
/// <c>languages_incomplete</c> / <c>language_not_allowed</c>), so for every usable version the BRD country languages are
/// the version languages — no per-version CRM call is needed. A draft / in-review version may hold fewer texts, but it
/// is already <c>not_approved</c>.</para>
/// </summary>
public sealed partial class KnowledgeController
{
    private const string ClaimReadPermission = "crm.claim.read";
    private const string ClaimCoveragePath = "/api/crm/content-composition/claims/coverage";

    /// <summary>The CRM content ↔ claim error codes (KnowledgeContentClaimErrors) shown as Claims-section field errors.</summary>
    internal static readonly IReadOnlyList<string> ClaimErrorCodes =
    [
        "claim_refs_too_many", "claim_ref_invalid", "claim_ref_duplicate", "claim_not_found", "claim_ref_mismatch",
        "claim_country_version_not_found", "claim_product_mismatch", "claim_not_approved", "claim_language_mismatch",
        "dependency_unavailable"
    ];

    /// <summary>Claim options for the content form: a core row per claim plus a row per country that has a version.
    /// No product ⇒ an empty list (claims are product-bound). CRM unreachable / forbidden ⇒ <c>{disabled, reason}</c>,
    /// never a silent empty list.</summary>
    [HttpGet("api/claim-options")]
    public async Task<IActionResult> ClaimOptions([FromQuery] Guid? productId, [FromQuery] string? languageCode,
        CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        if (productId is null || productId == Guid.Empty)
            return Json(new { disabled = false, requiresProduct = true, options = Array.Empty<object>() });

        var response = await SendGatewayAsync(HttpMethod.Get, $"{ClaimCoveragePath}?productId={productId}", null, ct);
        if (response is null) return Json(new { disabled = true, reason = "ClaimOptionsUnavailable" });
        if ((int)response.StatusCode == 403) return Json(new { disabled = true, reason = "ClaimPermissionMissing" });
        if ((int)response.StatusCode == 404) return Json(new { disabled = true, reason = "ClaimEndpointMissing" });
        if (!response.IsSuccessStatusCode) return Json(new { disabled = true, reason = "ClaimOptionsUnavailable" });

        IReadOnlyList<ClaimCoverageOptions.Row>? rows;
        try
        {
            rows = ClaimCoverageOptions.ReadRows(await response.Content.ReadAsStringAsync(ct));
            if (rows is null) return Json(new { disabled = true, reason = "ClaimOptionsUnavailable" });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Knowledge claim-options coverage parse failed.");
            return Json(new { disabled = true, reason = "ClaimOptionsUnavailable" });
        }

        // Unavailable language set ⇒ the language warning is skipped (CRM still gates publish), never guessed.
        var countryLanguages = await ReadCountryLanguagesAsync(ct);
        var language = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.Trim();
        var options = new List<object>();
        foreach (var row in rows)
        {
            var coreUsable = ClaimCoverageOptions.IsUsableStatus(row.CoreStatus);
            options.Add(new
            {
                claimId = row.ClaimId, claimCode = row.ClaimCode, claimName = row.ClaimName, kind = row.Kind,
                countryVersionId = (string?)null, countryCode = (string?)null, countryName = (string?)null,
                version = row.CoreVersion, status = row.CoreStatus,
                languages = Array.Empty<string>(),
                usable = coreUsable, reason = coreUsable ? null : ClaimCoverageOptions.NotApproved
            });

            // Only a country with a version can be bound (closed / not-opened / not-applicable carry none).
            foreach (var cell in row.Cells.Where(c => c.VersionId is not null))
            {
                var languages = countryLanguages is not null && countryLanguages.TryGetValue(cell.CountryCode, out var l) ? l : [];
                var reason = ClaimCoverageOptions.CountryReason(cell.State, language, countryLanguages is null ? null : languages);
                options.Add(new
                {
                    claimId = row.ClaimId, claimCode = row.ClaimCode, claimName = row.ClaimName, kind = row.Kind,
                    countryVersionId = cell.VersionId, countryCode = cell.CountryCode,
                    countryName = ClaimDisplayNames.CountryName(cell.CountryCode) ?? cell.CountryCode,
                    version = cell.Version, status = cell.State, languages,
                    usable = reason is null, reason
                });
            }
        }

        return Json(new { disabled = false, options });
    }

    /// <summary>The CRM write shape of the refs — always a list (never null), blank rows dropped.</summary>
    private static List<object> ToClaimRefPayload(IEnumerable<KnowledgeContentClaimRefViewModel>? refs) =>
        (refs ?? [])
        .Where(r => r.ClaimId != Guid.Empty && !string.IsNullOrWhiteSpace(r.ClaimCode))
        .Select(r => (object)new
        {
            ClaimCode = r.ClaimCode.Trim(),
            r.ClaimId,
            CountryVersionId = r.CountryVersionId is { } v && v != Guid.Empty ? v : (Guid?)null,
            CountryCode = r.CountryVersionId is { } cv && cv != Guid.Empty && !string.IsNullOrWhiteSpace(r.CountryCode)
                ? r.CountryCode.Trim().ToUpperInvariant()
                : null
        })
        .ToList();

    /// <summary>Splits the CRM <c>[code, message]</c> pairs of the claim codes out of the error list. The subject (which
    /// claim) is read from the message: <c>ClaimRefs[i]</c> points at the posted ref, <c>Claim 'CLM-…' (TR)</c> names it.</summary>
    private static (List<KnowledgeClaimRefErrorViewModel> Claim, List<string> Other) SplitClaimErrors(
        IReadOnlyList<string> errors, IReadOnlyList<KnowledgeContentClaimRefViewModel> refs)
    {
        var claim = new List<KnowledgeClaimRefErrorViewModel>();
        var rest = new List<string>();
        for (var i = 0; i < errors.Count; i++)
        {
            var code = errors[i]?.Trim() ?? string.Empty;
            if (!ClaimErrorCodes.Contains(code, StringComparer.Ordinal))
            {
                rest.Add(errors[i]!);
                continue;
            }

            string? message = null;
            if (i + 1 < errors.Count && !ClaimErrorCodes.Contains(errors[i + 1]?.Trim() ?? string.Empty, StringComparer.Ordinal))
            {
                message = errors[++i];
            }

            claim.Add(new KnowledgeClaimRefErrorViewModel { Code = code, Subject = ClaimErrorSubject(message, refs) });
        }

        return (claim, rest);
    }

    private static string? ClaimErrorSubject(string? message, IReadOnlyList<KnowledgeContentClaimRefViewModel> refs)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var indexed = Regex.Match(message, @"^ClaimRefs\[(\d+)\]");
        if (indexed.Success && int.TryParse(indexed.Groups[1].Value, out var index) && index >= 0 && index < refs.Count)
        {
            var r = refs[index];
            return string.IsNullOrWhiteSpace(r.CountryCode) ? r.ClaimCode : $"{r.ClaimCode} ({r.CountryCode})";
        }

        var named = Regex.Match(message, @"Claim '([^']+)'(?: \(([A-Za-z]{2,3})\))?");
        if (named.Success)
            return named.Groups[2].Success ? $"{named.Groups[1].Value} ({named.Groups[2].Value})" : named.Groups[1].Value;
        return null;
    }

    /// <summary>The detail page's "Linked claims" rows. Names and versions come from one coverage read (all claims),
    /// made only for a viewer with <c>crm.claim.read</c>; without it the rows keep the stored code and status and carry no
    /// link. A ref to a superseded claim / version keeps its own status (CRM filled it) and shows no current version.</summary>
    private async Task<List<KnowledgeLinkedClaimViewModel>> LoadLinkedClaimsAsync(KnowledgeContentDetailViewModel content,
        CancellationToken ct)
    {
        if (content.ClaimRefs.Count == 0) return [];
        var canReadClaims = HasAnyPermission(ClaimReadPermission);

        var rowsById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var rowsByCode = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (canReadClaims)
        {
            var response = await SendGatewayAsync(HttpMethod.Get, ClaimCoveragePath, null, ct);
            if (response is not null && response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                        && data.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var row in rows.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
                        {
                            if (GetFirstString(row, "claimId") is { } id) rowsById[id] = row.Clone();
                            if (GetFirstString(row, "claimCode") is { } code) rowsByCode[code] = row.Clone();
                        }
                    }
                }
                catch (JsonException ex) { _logger.LogWarning(ex, "Knowledge linked-claims coverage parse failed."); }
            }
        }

        return content.ClaimRefs.Select(r =>
        {
            rowsByCode.TryGetValue(r.ClaimCode, out var byCode);
            var hasRow = rowsById.TryGetValue(r.ClaimId.ToString(), out var row);
            var isCountry = r.CountryVersionId is { } vid && vid != Guid.Empty;
            string? version = null;
            string? currentStatus = null;
            if (isCountry && byCode.ValueKind == JsonValueKind.Object && byCode.TryGetProperty("cells", out var cells)
                && cells.ValueKind == JsonValueKind.Array)
            {
                var cell = cells.EnumerateArray().FirstOrDefault(c => c.ValueKind == JsonValueKind.Object
                    && string.Equals(GetFirstString(c, "versionId"), r.CountryVersionId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (cell.ValueKind == JsonValueKind.Object)
                {
                    version = GetFirstString(cell, "version");
                    currentStatus = GetFirstString(cell, "state");
                }
            }
            else if (!isCountry && hasRow)
            {
                version = GetFirstString(row, "coreVersion");
                currentStatus = GetFirstString(row, "coreStatus");
            }

            return new KnowledgeLinkedClaimViewModel
            {
                Ref = r,
                ClaimName = byCode.ValueKind == JsonValueKind.Object ? GetFirstString(byCode, "claimName") : null,
                Version = version,
                CountryName = isCountry && !string.IsNullOrWhiteSpace(r.CountryCode)
                    ? ClaimDisplayNames.CountryName(r.CountryCode.ToUpperInvariant()) ?? r.CountryCode
                    : null,
                Status = (isCountry ? r.CountryVersionStatus : r.ClaimStatus) ?? currentStatus,
                Href = !canReadClaims ? null
                    : isCountry ? $"/CRM/Claims/CountryVersions/{r.CountryVersionId}/Edit"
                    : $"/CRM/Claims/Edit/{r.ClaimId}"
            };
        }).ToList();
    }

    /// <summary>Country → content languages from the GLOBAL BRD set <c>country-content-languages</c> (read without
    /// scope_key; attribute <c>Languages</c>, comma separated). Null = the set is unavailable.</summary>
    private async Task<Dictionary<string, string[]>?> ReadCountryLanguagesAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get,
            "/api/v1/reference-data/sets/country-content-languages/published-values", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            JsonElement items;
            if (data.ValueKind == JsonValueKind.Array) items = data;
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var it)
                     && it.ValueKind == JsonValueKind.Array) items = it;
            else return null;

            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
            {
                if ((item.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False)
                    || (item.TryGetProperty("isDeprecated", out var dep) && dep.ValueKind == JsonValueKind.True)) continue;
                if (GetFirstString(item, "valueCode", "value_code", "code") is not { } code) continue;
                var list = item.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object
                    ? GetFirstString(attrs, "Languages", "languages")
                    : null;
                result[code.ToUpperInvariant()] = (list ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Knowledge country-content-languages parse failed.");
            return null;
        }
    }
}
