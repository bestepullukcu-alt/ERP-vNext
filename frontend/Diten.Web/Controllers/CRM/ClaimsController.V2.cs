using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CL-FE-1 — the claims v2 same-origin proxy layer (<c>/CRM/Claims/api/v2/…</c>). Every CrmService v2 claims route is
/// mirrored 1:1 (same path tail, same verb): reads need <c>crm.claim.read</c>, writes <c>crm.claim.manage</c>; CRM stays
/// the authoritative rule + permission layer, so bodies and <c>[code, message]</c> refusals pass through untouched and
/// bodiless statuses (204) stay bodiless. The direct approve routes are deliberately NOT mirrored — a claim is approved
/// only through its MOD-0023 round (submit-review). Lookups for the v2 screens live under <c>api/v2/lookups/…</c>.
/// </summary>
public sealed partial class ClaimsController
{
    private const string V2 = "api/v2";
    private const string ReferenceDataBase = "/api/v1/reference-data/sets";

    // ---------------- claims (list / detail / write / coverage / new version / closures) ----------------

    [HttpGet(V2 + "/claims")]
    public Task<IActionResult> V2List(CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}{Request.QueryString}", ReadPermission, ct);

    [HttpGet(V2 + "/claims/{claimId:guid}")]
    public Task<IActionResult> V2Get(Guid claimId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/{claimId}", ReadPermission, ct);

    [HttpPost(V2 + "/claims")]
    public Task<IActionResult> V2Create([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, ClaimsBase, body, ManagePermission, ct);

    [HttpPut(V2 + "/claims/{claimId:guid}")]
    public Task<IActionResult> V2Update(Guid claimId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ClaimsBase}/{claimId}", body, ManagePermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/archive")]
    public Task<IActionResult> V2Archive(Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/archive", null, ManagePermission, ct);

    [HttpGet(V2 + "/claims/coverage")]
    public Task<IActionResult> V2Coverage(CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/coverage{Request.QueryString}", ReadPermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/new-version")]
    public Task<IActionResult> V2NewVersion(Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/new-version", null, ManagePermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/country-closures")]
    public Task<IActionResult> V2CloseCountry(Guid claimId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/country-closures", body, ManagePermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/country-closures/{countryCode}/reopen")]
    public Task<IActionResult> V2ReopenCountry(Guid claimId, string countryCode, [FromBody] JsonElement? body,
        CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post,
            $"{ClaimsBase}/{claimId}/country-closures/{Uri.EscapeDataString(countryCode)}/reopen", body,
            ManagePermission, ct);

    // ---------------- country versions ----------------

    [HttpGet(V2 + "/claims/{claimId:guid}/country-versions")]
    public Task<IActionResult> V2ListCountryVersions(Guid claimId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/{claimId}/country-versions{Request.QueryString}", ReadPermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/country-versions")]
    public Task<IActionResult> V2CreateCountryVersion(Guid claimId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/country-versions", body, ManagePermission, ct);

    [HttpGet(V2 + "/claims/country-versions/{versionId:guid}")]
    public Task<IActionResult> V2GetCountryVersion(Guid versionId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/country-versions/{versionId}", ReadPermission, ct);

    [HttpPut(V2 + "/claims/country-versions/{versionId:guid}")]
    public Task<IActionResult> V2UpdateCountryVersion(Guid versionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ClaimsBase}/country-versions/{versionId}", body, ManagePermission, ct);

    [HttpPost(V2 + "/claims/country-versions/{versionId:guid}/new-version")]
    public Task<IActionResult> V2NewCountryVersion(Guid versionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/country-versions/{versionId}/new-version", null, ManagePermission, ct);

    [HttpPost(V2 + "/claims/country-versions/{versionId:guid}/archive")]
    public Task<IActionResult> V2ArchiveCountryVersion(Guid versionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/country-versions/{versionId}/archive", null, ManagePermission, ct);

    // ---------------- review rounds (MOD-0023; the caller's token submits, so SoD holds) ----------------

    [HttpPost(V2 + "/claims/{claimId:guid}/submit-review")]
    public Task<IActionResult> V2SubmitReview(Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/submit-review", null, ManagePermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/withdraw-review")]
    public Task<IActionResult> V2WithdrawReview(Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/withdraw-review", null, ManagePermission, ct);

    [HttpGet(V2 + "/claims/{claimId:guid}/review-history")]
    public Task<IActionResult> V2ReviewHistory(Guid claimId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/{claimId}/review-history", ReadPermission, ct);

    [HttpPost(V2 + "/claims/country-versions/{versionId:guid}/submit-review")]
    public Task<IActionResult> V2SubmitCountryVersionReview(Guid versionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/country-versions/{versionId}/submit-review", null, ManagePermission, ct);

    [HttpPost(V2 + "/claims/country-versions/{versionId:guid}/withdraw-review")]
    public Task<IActionResult> V2WithdrawCountryVersionReview(Guid versionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/country-versions/{versionId}/withdraw-review", null, ManagePermission, ct);

    [HttpGet(V2 + "/claims/country-versions/{versionId:guid}/review-history")]
    public Task<IActionResult> V2CountryVersionReviewHistory(Guid versionId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/country-versions/{versionId}/review-history", ReadPermission, ct);

    // ---------------- evidence (MOD-0031 via CRM) ----------------

    [HttpGet(V2 + "/claims/{claimId:guid}/evidence")]
    public Task<IActionResult> V2Evidence(Guid claimId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/{claimId}/evidence{Request.QueryString}", ReadPermission, ct);

    [HttpGet(V2 + "/claims/country-versions/{versionId:guid}/evidence")]
    public Task<IActionResult> V2CountryVersionEvidence(Guid versionId, CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/country-versions/{versionId}/evidence{Request.QueryString}", ReadPermission, ct);

    [HttpPost(V2 + "/claims/{claimId:guid}/evidence")]
    public Task<IActionResult> V2LinkEvidence(Guid claimId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/evidence", body, ManagePermission, ct);

    [HttpPost(V2 + "/claims/country-versions/{versionId:guid}/evidence")]
    public Task<IActionResult> V2LinkCountryVersionEvidence(Guid versionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/country-versions/{versionId}/evidence", body, ManagePermission, ct);

    [HttpPost(V2 + "/claims/evidence/{linkId:guid}/remove")]
    public Task<IActionResult> V2RemoveEvidence(Guid linkId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/evidence/{linkId}/remove", body, ManagePermission, ct);

    [HttpGet(V2 + "/claims/evidence/document-options")]
    public Task<IActionResult> V2EvidenceDocumentOptions(CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/evidence/document-options{Request.QueryString}", ReadPermission, ct);

    // ---------------- usage (WP-CL-BE-6) ----------------

    [HttpGet(V2 + "/claims/usage")]
    public Task<IActionResult> V2Usage(CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}/usage{Request.QueryString}", ReadPermission, ct);

    // ================= lookups =================

    /// <summary>The country axis: <c>COUNTRY_CODES</c> (global, BRD order) joined with <c>country-content-languages</c>
    /// (global, attribute Languages; first entry = default). Deprecated / inactive values are left out. The languages set
    /// being unavailable only empties <c>languages</c>; COUNTRY_CODES unavailable is a refusal (never a local list).</summary>
    [HttpGet(V2 + "/lookups/countries")]
    public async Task<IActionResult> V2LookupCountries(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        var countries = await ReadReferenceValuesAsync("COUNTRY_CODES", tenantScoped: false, ct);
        if (countries is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { errors = new[] { "reference_set_missing", "COUNTRY_CODES is not available." } });

        var languages = (await ReadReferenceValuesAsync("country-content-languages", tenantScoped: false, ct) ?? [])
            .ToDictionary(v => v.Code.ToUpperInvariant(), v => v, StringComparer.Ordinal);
        // WP-CL-FE-2 — display names from ICU in the request's UI culture (see ClaimDisplayNames). `languages` stays the
        // plain code array (FE-3 reads it); the named form is the additional `languageDetails`. A code ICU does not know
        // falls back to the BRD display name, then to the code itself.
        var data = countries.Select(c =>
        {
            var code = c.Code.ToUpperInvariant();
            var codes = languages.TryGetValue(code, out var l) && l.Attributes.TryGetValue("Languages", out var list)
                ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();
            return new
            {
                code,
                name = ClaimDisplayNames.CountryName(code) ?? c.Name ?? code,
                nativeName = ClaimDisplayNames.CountryNativeName(code, codes.FirstOrDefault()) ?? c.Name ?? code,
                languages = codes,
                languageDetails = codes.Select(lang => new
                {
                    code = lang,
                    name = ClaimDisplayNames.LanguageName(lang) ?? lang,
                    nativeName = ClaimDisplayNames.LanguageNativeName(lang) ?? lang
                }).ToList()
            };
        }).ToList();
        return Ok(new { data });
    }

    [HttpGet(V2 + "/lookups/closure-reasons")]
    public Task<IActionResult> V2LookupClosureReasons(CancellationToken ct) =>
        ReferenceLookupAsync("claim-country-closure-reason", tenantScoped: true, ct);

    [HttpGet(V2 + "/lookups/adaptation-types")]
    public Task<IActionResult> V2LookupAdaptationTypes(CancellationToken ct) =>
        ReferenceLookupAsync("claim-adaptation-type", tenantScoped: true, ct);

    [HttpGet(V2 + "/lookups/evidence-types")]
    public Task<IActionResult> V2LookupEvidenceTypes(CancellationToken ct) =>
        ReferenceLookupAsync("evidence-type", tenantScoped: false, ct);

    /// <summary>MDM global product selector (Knowledge pattern). Returns <c>{disabled, reason}</c> instead of a silent
    /// empty list when MDM is unreachable, the endpoint is missing or the caller lacks the MDM read permission.</summary>
    [HttpGet(V2 + "/lookups/products")]
    public async Task<IActionResult> V2LookupProducts(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/global-products/selector{Request.QueryString}", null, ct);
        if (response is null) return Ok(new { disabled = true, reason = "GlobalProductPickerUnavailable" });
        var status = (int)response.StatusCode;
        if (status == 404) return Ok(new { disabled = true, reason = "GlobalProductEndpointMissing" });
        if (status is 401 or 403) return Ok(new { disabled = true, reason = "GlobalProductPermissionMissing" });
        if (!response.IsSuccessStatusCode) return Ok(new { disabled = true, reason = "GlobalProductPickerUnavailable" });
        return await ToProxyResultAsync(response, ct);
    }

    [HttpGet(V2 + "/lookups/audience-profiles")]
    public Task<IActionResult> V2LookupAudienceProfiles(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/audience-profiles{Request.QueryString}", ReadPermission, ct);

    [HttpGet(V2 + "/lookups/org-units")]
    public Task<IActionResult> V2LookupOrgUnits(CancellationToken ct) =>
        ProxyGetAsync($"/api/platform/organization-units{Request.QueryString}", ReadPermission, ct);

    /// <summary>
    /// The approval flow preview of a MOD-0023 template (by code): its stage/step names and the candidate position
    /// names. Built from existing Platform reads only (definitions → definition detail → active published version →
    /// position lookup) on the caller's token. Returns <c>{available:false, reason}</c> when the template is missing,
    /// unpublished or the caller cannot read workflow definitions — never a guessed flow.
    /// </summary>
    [HttpGet(V2 + "/lookups/workflow-template")]
    public async Task<IActionResult> V2LookupWorkflowTemplate([FromQuery] string? code, CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { errors = new[] { "required", "code is required." } });

        var list = await ReadDataAsync("/api/v1/workflow/definitions", ct);
        if (list.Status is 401 or 403) return Ok(new { data = new { available = false, reason = "WorkflowPermissionMissing" } });
        if (list.Data is not { ValueKind: JsonValueKind.Array } definitions)
            return Ok(new { data = new { available = false, reason = "WorkflowUnavailable" } });

        var definition = definitions.EnumerateArray().FirstOrDefault(d =>
            string.Equals(GetFirstString(d, "templateCode"), code.Trim(), StringComparison.OrdinalIgnoreCase));
        if (definition.ValueKind != JsonValueKind.Object || GetFirstString(definition, "id") is not { } definitionId)
            return Ok(new { data = new { available = false, reason = "WorkflowTemplateMissing" } });

        var detail = await ReadDataAsync($"/api/v1/workflow/definitions/{definitionId}", ct);
        var versionId = detail.Data is { ValueKind: JsonValueKind.Object } d2
            ? GetFirstString(d2, "activePublishedVersionId")
            : null;
        if (versionId is null)
            return Ok(new { data = new { available = false, reason = "WorkflowTemplateNotPublished" } });

        var version = await ReadDataAsync($"/api/v1/workflow/definitions/{definitionId}/versions/{versionId}", ct);
        if (version.Data is not { ValueKind: JsonValueKind.Object } v || GetFirstString(v, "definitionJson") is not { } json)
            return Ok(new { data = new { available = false, reason = "WorkflowUnavailable" } });

        var positions = await ReadDataAsync("/api/v1/workflow/lookups/positions", ct);
        var positionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (positions.Data is { ValueKind: JsonValueKind.Array } rows)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (GetFirstString(row, "id") is { } id) positionNames[id] = GetFirstString(row, "name", "code") ?? id;
            }
        }

        try
        {
            using var plan = JsonDocument.Parse(json);
            var stages = plan.RootElement.TryGetProperty("stages", out var s) && s.ValueKind == JsonValueKind.Array
                ? s.EnumerateArray().Select(stage => new
                {
                    code = GetFirstString(stage, "code"),
                    name = GetFirstString(stage, "name", "code"),
                    steps = stage.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array
                        ? steps.EnumerateArray().Select(step => new
                        {
                            code = GetFirstString(step, "code"),
                            name = GetFirstString(step, "name", "code"),
                            candidates = CandidateNames(step, positionNames)
                        }).ToList()
                        : []
                }).ToList()
                : [];
            return Ok(new { data = new { available = true, templateCode = code.Trim(), stages } });
        }
        catch (JsonException)
        {
            return Ok(new { data = new { available = false, reason = "WorkflowUnavailable" } });
        }
    }

    [HttpGet(V2 + "/lookups/workflow-history/{instanceId:guid}")]
    public Task<IActionResult> V2LookupWorkflowHistory(Guid instanceId, CancellationToken ct) =>
        ProxyGetAsync($"/api/v1/workflow/instances/{instanceId}/history", ReadPermission, ct);

    // ---------------- lookup helpers ----------------

    private static List<string> CandidateNames(JsonElement step, IReadOnlyDictionary<string, string> positionNames)
    {
        var names = new List<string>();
        if (!step.TryGetProperty("assignment", out var assignment)
            || !assignment.TryGetProperty("candidatePrincipalIds", out var ids) || ids.ValueKind != JsonValueKind.Array)
            return names;

        foreach (var raw in ids.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!))
        {
            var id = raw.StartsWith("position:", StringComparison.OrdinalIgnoreCase) ? raw["position:".Length..] : raw;
            names.Add(positionNames.TryGetValue(id, out var name) ? name : id);
        }

        return names;
    }

    private async Task<IActionResult> ReferenceLookupAsync(string setCode, bool tenantScoped, CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        var values = await ReadReferenceValuesAsync(setCode, tenantScoped, ct);
        if (values is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { errors = new[] { "reference_set_missing", $"{setCode} is not available." } });
        return Ok(new { data = values.Select(v => new { code = v.Code, name = v.Name ?? v.Code }).ToList() });
    }

    private sealed record ReferenceValue(string Code, string? Name, IReadOnlyDictionary<string, string> Attributes);

    /// <summary>Published values of a BRD set, active only, in BRD order. A GLOBAL set is read WITHOUT scope_key, a
    /// tenant set WITH the JWT tenant (never the client's) — the platform refuses the other shape. Null = unavailable.</summary>
    private async Task<IReadOnlyList<ReferenceValue>?> ReadReferenceValuesAsync(string setCode, bool tenantScoped,
        CancellationToken ct)
    {
        var path = $"{ReferenceDataBase}/{Uri.EscapeDataString(setCode)}/published-values";
        if (tenantScoped)
        {
            path += $"?scope_key={Uri.EscapeDataString(GetTenantId() ?? string.Empty)}";
        }

        var reply = await ReadDataAsync(path, ct);
        JsonElement items;
        if (reply.Data is { ValueKind: JsonValueKind.Object } data && data.TryGetProperty("items", out var it)
            && it.ValueKind == JsonValueKind.Array) items = it;
        else if (reply.Data is { ValueKind: JsonValueKind.Array } arr) items = arr;
        else return null;

        var values = new List<(ReferenceValue Value, int Order, int Index)>();
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object) continue;
            if ((item.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False)
                || (item.TryGetProperty("isDeprecated", out var dep) && dep.ValueKind == JsonValueKind.True)) continue;
            if (GetFirstString(item, "valueCode", "value_code", "code") is not { } code) continue;

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (item.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in attrs.EnumerateObject())
                    attributes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
            }

            var order = item.TryGetProperty("sortOrder", out var so) && so.TryGetInt32(out var n) ? n : int.MaxValue;
            values.Add((new ReferenceValue(code, GetFirstString(item, "displayName", "display_name", "name"), attributes),
                order, index));
        }

        return values.OrderBy(v => v.Order).ThenBy(v => v.Index).Select(v => v.Value).ToList();
    }

    private sealed record GatewayData(int Status, JsonElement? Data);

    private async Task<GatewayData> ReadDataAsync(string path, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null) return new GatewayData(502, null);
        var status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode) return new GatewayData(status, null);
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return new GatewayData(status, root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d)
                ? d.Clone()
                : root.Clone());
        }
        catch (JsonException)
        {
            return new GatewayData(status, null);
        }
    }
}
