using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-UI-1 — the Knowledge Path Studio surface (list rows, workspace, lookups and the KP-1 write proxies). Everything
/// the browser shows is resolved here: country / language names from ICU (<see cref="ClaimDisplayNames"/> through
/// <see cref="ReferenceValueSet"/>), content-type / status / reason labels from the KnowledgePathStudio resx — the page
/// never prints a raw code. CRM stays the authority for every rule; the "cannot add" reasons here only explain what CRM
/// would refuse (language, product, not published, slot full).
/// </summary>
public sealed partial class KnowledgePathsController
{
    private const string GlobalProductSource = "global-product";

    /// <summary>The CRM (WP-KP-1) error codes the studio shows as user text (Err_{code}); never shown raw.</summary>
    public static readonly IReadOnlyList<string> StudioErrorCodes =
    [
        "chain_template_invalid", "chain_template_required", "chain_subject_mismatch", "path_identity_locked",
        "country_invalid", "language_not_in_country", "reference_set_unavailable", "chain_slot_invalid",
        "chain_slot_full", "chain_slot_move_forbidden", "component_language_mismatch", "claim_product_mismatch",
        "claim_ref_duplicate"
    ];

    /// <summary>Why a content cannot be added to a slot (AddReason_{code}).</summary>
    internal static class AddReasons
    {
        public const string NotPublished = "not_published";
        public const string Language = "language";
        public const string Product = "product";
        public const string SlotFull = "slot_full";
        public const string OnPath = "on_path";
    }

    // ---------------- KP-1 write proxies (manage) ----------------

    [HttpPost("api/paths")]
    public Task<IActionResult> CreatePath([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/paths", body, ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/bind-chain")]
    public Task<IActionResult> BindChain(Guid pathId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/bind-chain", body, ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/claims")]
    public Task<IActionResult> AddClaim(Guid pathId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/claims", body, ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/claims/{claimId:guid}/arrange")]
    public Task<IActionResult> ArrangeClaim(Guid pathId, Guid claimId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/claims/{claimId}/arrange", body,
            ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/claims/{claimId:guid}/remove")]
    public Task<IActionResult> RemoveClaim(Guid pathId, Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/claims/{claimId}/remove", null,
            ManagePermission, ct, ManageFallback);

    // ---------------- lookups (read) ----------------

    /// <summary>Bindable chains only: published, not archived and branched (every branch with a code and at least one
    /// step) — the rule KP-1 enforces (<c>chain_template_invalid</c>). Optional <c>subjectId</c> narrows to one subject
    /// (bind-chain needs the path's subject).</summary>
    [HttpGet("api/lookups/chain-templates")]
    public async Task<IActionResult> ChainTemplateLookup([FromQuery] Guid? subjectId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var query = "?status=published&includeArchived=false" + (subjectId is { } s && s != Guid.Empty ? $"&subjectId={s}" : "");
        var templates = await ReadItemsAsync($"/api/crm/knowledge/concept-chain-templates{query}", ct);
        if (templates is null) return Unavailable();

        var subjects = await SubjectsAsync(ct);
        var types = await NamesAsync("/api/crm/knowledge/concept-types?includeArchived=true", "conceptTypeId", "conceptTypeName", ct);
        var profiles = await NamesAsync("/api/crm/knowledge/audience-profiles?includeArchived=true", "audienceProfileId", "profileName", ct);

        var data = templates.Where(IsBindable).Select(t =>
        {
            var subject = subjects.GetValueOrDefault(Str(t, "subjectId") ?? string.Empty);
            return new
            {
                id = Str(t, "conceptChainTemplateId"),
                code = Str(t, "chainCode"),
                name = Str(t, "chainName"),
                version = Str(t, "chainVersion"),
                subjectId = Str(t, "subjectId"),
                subjectName = subject?.Name,
                productName = subject?.ProductName,
                audiences = Guids(t, "forWhomAudienceProfileIds").Select(id => profiles.GetValueOrDefault(id)).OfType<string>().ToList(),
                branches = Branches(t).Select(b => new
                {
                    code = b.Code,
                    name = b.Name,
                    steps = b.Steps.Select(st => new { chainStepId = st.Id, name = types.GetValueOrDefault(st.Id), min = st.Min, max = st.Max }).ToList()
                }).ToList()
            };
        }).OrderBy(t => t.name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return Ok(new { data });
    }

    /// <summary>The country axis (COUNTRY_CODES + country-content-languages) with ICU names and native language names.</summary>
    [HttpGet("api/lookups/countries")]
    public async Task<IActionResult> CountryLookup(CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var countries = ReferenceValueSet.Parse(await ReadReferenceSetDataAsync("COUNTRY_CODES", ct));
        if (countries is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { errors = new[] { "reference_set_unavailable", "COUNTRY_CODES is not available." } });
        var languages = ReferenceValueSet.Parse(
            await ReadReferenceSetDataAsync("country-content-languages", ct));
        return Ok(new
        {
            data = ReferenceValueSet.Countries(countries, languages).Select(c => new
            {
                code = c.Code,
                name = c.Name,
                nativeName = c.NativeName,
                languages = c.Languages,
                languageDetails = c.LanguageDetails.Select(l => new { code = l.Code, name = l.Name, nativeName = l.NativeName }).ToList()
            }).ToList()
        });
    }

    /// <summary>Contents that could go onto a slot of a chain-bound path: the path subject's and the path product's
    /// non-archived contents, each with "addable" or the reason CRM would refuse it (not published, another language,
    /// another product, slot full). Labels only — never a raw type / language / status code.</summary>
    [HttpGet("api/lookups/path-contents")]
    public async Task<IActionResult> PathContentLookup(
        [FromQuery] Guid pathId, [FromQuery] string? branchCode, [FromQuery] Guid? chainStepId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var path = await ReadDataAsync($"/api/crm/knowledge/paths/{pathId}", ct);
        if (path is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });

        var language = Str(p, "languageCode");
        var productId = p.TryGetProperty("derivedContext", out var dc) && dc.ValueKind == JsonValueKind.Object ? Str(dc, "productId") : null;
        var subjectId = Str(p, "subjectId");
        var slotFull = SlotIsFull(p, branchCode, chainStepId);

        var rows = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { subjectId is null ? null : $"subjectId={subjectId}", productId is null ? null : $"productId={productId}" })
        {
            if (source is null) continue;
            foreach (var item in await ReadItemsAsync($"/api/crm/knowledge/contents?{source}&includeArchived=false", ct) ?? [])
            {
                if (Str(item, "contentId") is { } id && !Bool(item, "isArchived")) rows.TryAdd(id, item);
            }
        }

        var data = rows.Values.Select(c =>
        {
            var reason = ContentReason(c, language, productId, slotFull);
            var type = Str(c, "contentType");
            return new
            {
                contentId = Str(c, "contentId"),
                title = Str(c, "contentTitle") ?? Str(c, "contentCode"),
                typeLabel = TypeLabel(type),
                defaultStepType = DefaultStepType(type),
                languageName = LanguageName(Str(c, "languageCode")),
                statusLabel = StatusLabel(Str(c, "contentStatus")),
                addable = reason is null,
                reason,
                reasonLabel = reason is null ? null : Label("AddReason_" + reason)
            };
        }).OrderBy(c => c.addable ? 0 : 1).ThenBy(c => c.title, StringComparer.CurrentCultureIgnoreCase).ToList();
        return Ok(new { data });
    }

    /// <summary>The path product's claims (one coverage read — the FE-5 rules, <see cref="ClaimCoverageOptions"/>), each
    /// with its version in the PATH country: text / qualifier in the path language, status and usable / reason
    /// (not approved, no country version, language). <c>q</c> matches code, name or text. An unusable claim may still
    /// be placed (CRM accepts it; the release decides) — only a claim already on the path is not addable.</summary>
    [HttpGet("api/lookups/path-claims")]
    public async Task<IActionResult> PathClaimLookup([FromQuery] Guid pathId, [FromQuery] string? q, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var path = await ReadDataAsync($"/api/crm/knowledge/paths/{pathId}", ct);
        if (path is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });

        var productId = p.TryGetProperty("derivedContext", out var dc) && dc.ValueKind == JsonValueKind.Object ? Str(dc, "productId") : null;
        if (productId is null) return Json(new { disabled = true, reason = "ClaimNoProduct", options = Array.Empty<object>() });
        var country = Str(p, "countryCode")?.ToUpperInvariant();
        var language = Str(p, "languageCode");
        var onPath = (p.TryGetProperty("claims", out var placed) && placed.ValueKind == JsonValueKind.Array
                ? placed.EnumerateArray().Select(x => Str(x, "claimId")).OfType<string>()
                : [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/content-composition/claims/coverage?productId={productId}", null, ct);
        if (response is null) return Json(new { disabled = true, reason = "ClaimOptionsUnavailable", options = Array.Empty<object>() });
        if ((int)response.StatusCode == 403) return Json(new { disabled = true, reason = "ClaimPermissionMissing", options = Array.Empty<object>() });
        if (!response.IsSuccessStatusCode) return Json(new { disabled = true, reason = "ClaimOptionsUnavailable", options = Array.Empty<object>() });

        IReadOnlyList<ClaimCoverageOptions.Row>? rows;
        try { rows = ClaimCoverageOptions.ReadRows(await response.Content.ReadAsStringAsync(ct)); }
        catch (JsonException) { rows = null; }
        if (rows is null) return Json(new { disabled = true, reason = "ClaimOptionsUnavailable", options = Array.Empty<object>() });

        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var options = new List<object>();
        foreach (var row in rows)
        {
            var cell = country is null ? null : row.Cells.FirstOrDefault(c => c.CountryCode == country && c.VersionId is not null);
            string? text = null, qualifier = null, reason;
            if (cell is null)
            {
                reason = ClaimCoverageOptions.NoCountryVersion;
            }
            else
            {
                var version = await ReadDataAsync($"/api/crm/content-composition/claims/country-versions/{cell.VersionId}", ct);
                var texts = LocalizedTexts(version, "texts");
                text = texts.FirstOrDefault(t => SameLanguage(t.Language, language)).Text;
                qualifier = LocalizedTexts(version, "qualifiers").FirstOrDefault(t => SameLanguage(t.Language, language)).Text;
                reason = ClaimCoverageOptions.CountryReason(cell.State, language, texts.Select(t => t.Language).ToList());
            }

            if (term is not null
                && !row.ClaimCode.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                && !row.ClaimName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                && !(text?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)) continue;

            var isOnPath = onPath.Contains(row.ClaimId);
            options.Add(new
            {
                claimId = row.ClaimId,
                code = row.ClaimCode,
                name = row.ClaimName,
                text,
                qualifier,
                countryVersion = cell?.Version,
                statusLabel = cell is null ? null : ClaimStateLabel(cell.State),
                usable = reason is null,
                reason,
                reasonLabel = reason is null ? null : Label("Reason_" + reason),
                onPath = isOnPath,
                addable = !isOnPath,
                addReasonLabel = isOnPath ? Label("AddReason_" + AddReasons.OnPath) : null
            });
        }

        return Json(new { disabled = false, options });
    }

    // ---------------- studio reads ----------------

    /// <summary>List rows of the studio list: path + product (chain → subject → primary Global Product; a legacy path:
    /// its subject's) + country / language NAMES + chain name / version + status label + the legacy mark.</summary>
    [HttpGet("api/studio/paths")]
    public async Task<IActionResult> StudioPathList(CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var paths = await ReadItemsAsync("/api/crm/knowledge/paths?includeArchived=true", ct);
        if (paths is null) return Unavailable();

        var subjects = await SubjectsAsync(ct);
        var templates = (await ReadItemsAsync("/api/crm/knowledge/concept-chain-templates?includeArchived=true", ct) ?? [])
            .Where(t => Str(t, "conceptChainTemplateId") is not null)
            .GroupBy(t => Str(t, "conceptChainTemplateId")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var data = paths.Select(row =>
        {
            var chainId = Str(row, "chainTemplateId");
            var template = chainId is not null && templates.TryGetValue(chainId, out var t) ? t : (JsonElement?)null;
            var subjectId = template is { } tpl ? Str(tpl, "subjectId") : Str(row, "subjectId");
            var subject = subjectId is null ? null : subjects.GetValueOrDefault(subjectId);
            var country = Str(row, "countryCode");
            var language = Str(row, "languageCode");
            var status = Str(row, "pathStatus");
            var legacy = !row.TryGetProperty("isLegacyUnapproved", out var l) || l.ValueKind != JsonValueKind.False;
            return new
            {
                pathId = Str(row, "pathId"),
                pathCode = Str(row, "pathCode"),
                pathName = Str(row, "pathName"),
                pathVersion = Str(row, "pathVersion"),
                status,
                statusLabel = StatusLabel(status),
                isArchived = Bool(row, "isArchived"),
                isLegacyUnapproved = legacy,
                subjectId = Str(row, "subjectId"),
                productName = subject?.ProductName,
                countryCode = country,
                countryName = country is null ? null : ClaimDisplayNames.CountryName(country.ToUpperInvariant()) ?? country,
                languageCode = language,
                languageName = LanguageName(language),
                chainName = template is { } c ? Str(c, "chainName") : null,
                chainVersion = template is { } v ? Str(v, "chainVersion") : null,
                updatedAt = Str(row, "updatedAt") ?? Str(row, "createdAt")
            };
        }).ToList();
        return Ok(new { data });
    }

    /// <summary>The workspace model: header + single status line, identity strip (chain / country / language names,
    /// product, audiences), the chain's branches with their slots (min / max / conformance from CRM) and the items on
    /// each slot (contents with type / language / status LABELS, claims with the path-language text and reason), the
    /// field order (server StepOrder) and the edit flags. A legacy path carries its flat step list instead.</summary>
    [HttpGet("api/studio/paths/{pathId:guid}")]
    public async Task<IActionResult> StudioWorkspace(Guid pathId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var detail = await ReadDataAsync($"/api/crm/knowledge/paths/{pathId}", ct);
        if (detail is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });

        var status = Str(p, "pathStatus");
        var legacy = !p.TryGetProperty("isLegacyUnapproved", out var l) || l.ValueKind != JsonValueKind.False;
        var frozen = Bool(p, "isStepSetFrozen");
        var archived = Bool(p, "isArchived");
        var canManage = HasAnyPermission(ManagePermission, ManageFallback);
        var language = Str(p, "languageCode");
        var country = Str(p, "countryCode");

        var steps = Items(p, "steps")
            .Where(x => x.ValueKind == JsonValueKind.Object && !Bool(x, "isArchived"))
            .OrderBy(x => Int(x, "stepOrder") ?? int.MaxValue)
            .Select(x => x.Clone())
            .ToList();
        var contents = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var contentId in steps.Select(x => Str(x, "contentId")).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await ReadDataAsync($"/api/crm/knowledge/contents/{contentId}", ct) is { ValueKind: JsonValueKind.Object } c) contents[contentId] = c;
        }

        object? context = null;
        var branches = new List<object>();
        var sequence = new List<object>();
        if (!legacy && p.TryGetProperty("chainTemplate", out var chain) && chain.ValueKind == JsonValueKind.Object)
        {
            var template = await ReadDataAsync($"/api/crm/knowledge/concept-chain-templates/{Str(chain, "id")}", ct);
            var types = template is { } tt && Str(tt, "subjectId") is { } subjectId
                ? await NamesAsync($"/api/crm/knowledge/concept-types?subjectId={subjectId}&includeArchived=true", "conceptTypeId", "conceptTypeName", ct)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var derived = p.TryGetProperty("derivedContext", out var dc) && dc.ValueKind == JsonValueKind.Object ? dc : (JsonElement?)null;
            context = new
            {
                chainName = Str(chain, "name"),
                chainVersion = Str(chain, "version"),
                countryName = country is null ? null : ClaimDisplayNames.CountryName(country.ToUpperInvariant()) ?? country,
                languageName = LanguageName(language),
                languageNativeName = language is null ? null : ReferenceValueSet.LanguageOf(language).NativeName,
                productName = derived is { } d ? Str(d, "productName") ?? Str(d, "productCode") : null,
                audiences = derived is { } a && a.TryGetProperty("audiences", out var aud) && aud.ValueKind == JsonValueKind.Array
                    ? aud.EnumerateArray().Select(x => Str(x, "profileName") ?? Str(x, "profileCode")).OfType<string>().ToList()
                    : []
            };

            var conformance = Items(p, "chainConformance")
                .Where(x => x.ValueKind == JsonValueKind.Object).Select(x => x.Clone()).ToList();
            var claims = Items(p, "claims")
                .Where(x => x.ValueKind == JsonValueKind.Object).Select(x => x.Clone()).ToList();
            var branchNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var slotNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var branch in template is { } tpl ? Branches(tpl) : [])
            {
                branchNames[branch.Code] = branch.Name;
                var slots = branch.Steps.Select(st =>
                {
                    var name = types.GetValueOrDefault(st.Id);
                    slotNames[SlotKey(branch.Code, st.Id)] = name ?? string.Empty;
                    var conf = conformance.FirstOrDefault(c => SameSlot(c, branch.Code, st.Id));
                    var confStatus = conf.ValueKind == JsonValueKind.Object ? Str(conf, "status") : null;
                    var slotSteps = steps.Where(x => ArrangedIn(x, branch.Code, st.Id)).ToList();
                    var items = slotSteps.Select(x => (Position: ArrangementPosition(x), Kind: 0, Item: ContentItem(x, contents, steps)))
                        .Concat(claims.Where(x => ArrangedIn(x, branch.Code, st.Id)).Select(x => (Position: ArrangementPosition(x), Kind: 1, Item: ClaimItem(x))))
                        .OrderBy(x => x.Position).ThenBy(x => x.Kind).Select(x => x.Item).ToList();
                    return new
                    {
                        chainStepId = st.Id,
                        name,
                        min = st.Min,
                        max = st.Max,
                        count = conf.ValueKind == JsonValueKind.Object ? Int(conf, "count") ?? slotSteps.Count : slotSteps.Count,
                        status = confStatus,
                        statusLabel = confStatus is null ? null : Label("Conf_" + confStatus),
                        isFull = st.Max is { } max && slotSteps.Count >= max,
                        // WP-E2E-FIX-2 (E3-B1) — the slot's own rule (chain step MinSelection), never its current
                        // contents: an empty slot of a min>0 step is just as required as a filled one.
                        required = st.Min > 0,
                        durationMinutes = slotSteps.Sum(x => Int(x, "estimatedDurationMinutes") ?? 0),
                        items
                    };
                }).ToList();
                branches.Add(new { code = branch.Code, name = branch.Name, slots });
            }

            sequence.AddRange(steps.Select((x, i) =>
            {
                var arrangement = x.TryGetProperty("arrangement", out var ar) && ar.ValueKind == JsonValueKind.Object ? ar : (JsonElement?)null;
                var code = arrangement is { } aa ? Str(aa, "branchCode") : null;
                var stepId = arrangement is { } ab ? Str(ab, "chainStepId") : null;
                return (object)new
                {
                    order = i + 1,
                    title = ContentTitle(x, contents),
                    branchName = code is null ? null : branchNames.GetValueOrDefault(code, code),
                    slotName = code is null || stepId is null ? null : slotNames.GetValueOrDefault(SlotKey(code, stepId))
                };
            }));
        }

        // "Sahada vX" on the same status line: the published version of the same path code, when it is not this one.
        string? liveVersion = null;
        if (Str(p, "pathCode") is { } pathCode)
        {
            liveVersion = (await ReadItemsAsync($"/api/crm/knowledge/paths?pathCode={Uri.EscapeDataString(pathCode)}&includeArchived=false", ct) ?? [])
                .Where(x => !string.Equals(Str(x, "pathId"), Str(p, "pathId"), StringComparison.OrdinalIgnoreCase)
                            && string.Equals(Str(x, "pathStatus"), "published", StringComparison.OrdinalIgnoreCase))
                .Select(x => Str(x, "pathVersion"))
                .FirstOrDefault();
        }

        // WP-KP-UI-2 — the Compliance tab + the submit checklist (kurgu rules until KP-UI-3), labelled rows only.
        var compliance = legacy ? [] : await ComplianceForAsync(p, ct);

        var model = new
        {
            pathId = Str(p, "pathId"),
            liveVersion,
            canPublish = HasAnyPermission(PublishPermission),
            compliance = new
            {
                blockers = compliance.Count(r => r.Severity == KnowledgePathStudioReview.Blocker),
                warnings = compliance.Count(r => r.Severity == KnowledgePathStudioReview.Warning),
                rows = compliance.Select(r => new
                {
                    rule = r.Rule, severity = r.Severity, ruleLabel = r.RuleLabel, severityLabel = r.SeverityLabel, where = r.Where,
                    message = r.Message, fix = r.Fix, branchCode = r.BranchCode, chainStepId = r.ChainStepId, itemKind = r.ItemKind,
                    itemId = r.ItemId
                }).ToList()
            },
            pathCode = Str(p, "pathCode"),
            pathName = Str(p, "pathName"),
            pathVersion = Str(p, "pathVersion"),
            status,
            statusLabel = StatusLabel(status),
            isLegacyUnapproved = legacy,
            isArchived = archived,
            isFrozen = frozen,
            subjectId = Str(p, "subjectId"),
            canManage,
            canEdit = canManage && !legacy && !frozen && !archived
                      && string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase),
            canBind = canManage && legacy && !frozen && !archived
                      && string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase),
            context,
            branches,
            sequence,
            legacySteps = legacy
                ? steps.Select((x, i) => new { order = i + 1, title = Str(x, "stepTitle"), contentTitle = ContentTitle(x, contents) }).ToList<object>()
                : []
        };
        return Ok(new { data = model });
    }

    // ---------------- helpers ----------------

    private object ContentItem(JsonElement step, IReadOnlyDictionary<string, JsonElement> contents, IReadOnlyList<JsonElement> steps)
    {
        var content = Str(step, "contentId") is { } id && contents.TryGetValue(id, out var c) ? c : (JsonElement?)null;
        var prerequisite = Str(step, "prerequisiteStepId");
        var contentStatus = content is { } cs ? Str(cs, "contentStatus") : null;
        return new
        {
            kind = "content",
            stepId = Str(step, "stepId"),
            stepCode = Str(step, "stepCode"),
            stepTitle = Str(step, "stepTitle"),
            stepType = Str(step, "stepType"),
            contentId = Str(step, "contentId"),
            versionPinPolicy = Str(step, "versionPinPolicy"),
            completionRule = Str(step, "completionRule"),
            conceptNodeId = Str(step, "conceptNodeId"),
            notes = Str(step, "notes"),
            // Carried so the full-replace step update keeps them (the studio never edits branch conditions).
            branchConditions = Items(step, "branchConditions").Select(b => new
            {
                conditionCode = Str(b, "conditionCode"), description = Str(b, "description"), targetStepId = Str(b, "targetStepId")
            }).ToList(),
            isRequired = Bool(step, "isRequired"),
            durationMinutes = Int(step, "estimatedDurationMinutes"),
            prerequisiteStepId = prerequisite,
            prerequisiteLabel = prerequisite is null ? null
                : steps.FirstOrDefault(x => string.Equals(Str(x, "stepId"), prerequisite, StringComparison.OrdinalIgnoreCase)) is { ValueKind: JsonValueKind.Object } pre
                    ? ContentTitle(pre, contents) : null,
            stepOrder = Int(step, "stepOrder"),
            position = ArrangementPosition(step),
            title = ContentTitle(step, contents),
            typeLabel = TypeLabel(content is { } ct ? Str(ct, "contentType") : null),
            languageName = LanguageName(content is { } cl ? Str(cl, "languageCode") : null),
            statusLabel = StatusLabel(contentStatus),
            published = string.Equals(contentStatus, "published", StringComparison.OrdinalIgnoreCase)
        };
    }

    private object ClaimItem(JsonElement claim)
    {
        var reason = Str(claim, "reason");
        return new
        {
            kind = "claim",
            claimId = Str(claim, "claimId"),
            code = Str(claim, "claimCode"),
            name = Str(claim, "name"),
            text = Str(claim, "text"),
            qualifier = Str(claim, "qualifier"),
            countryVersion = Str(claim, "countryVersion"),
            statusLabel = Str(claim, "status") is { } st ? ClaimStateLabel(st) : null,
            usable = claim.TryGetProperty("usable", out var u) && u.ValueKind == JsonValueKind.True,
            reasonLabel = reason is null ? null : Label("Reason_" + reason),
            position = ArrangementPosition(claim)
        };
    }

    private static string? ContentTitle(JsonElement step, IReadOnlyDictionary<string, JsonElement> contents)
        => Str(step, "contentId") is { } id && contents.TryGetValue(id, out var c)
            ? Str(c, "contentTitle") ?? Str(step, "stepTitle")
            : Str(step, "resolvedContentTitle") ?? Str(step, "stepTitle");

    private static string? ContentReason(JsonElement content, string? language, string? productId, bool slotFull)
    {
        if (!string.Equals(Str(content, "contentStatus"), "published", StringComparison.OrdinalIgnoreCase)) return AddReasons.NotPublished;
        if (!SameLanguage(Str(content, "languageCode"), language)) return AddReasons.Language;
        if (productId is not null && Str(content, "productId") is { } cp
            && !string.Equals(cp, productId, StringComparison.OrdinalIgnoreCase)) return AddReasons.Product;
        return slotFull ? AddReasons.SlotFull : null;
    }

    private static bool SlotIsFull(JsonElement path, string? branchCode, Guid? chainStepId)
    {
        if (string.IsNullOrWhiteSpace(branchCode) || chainStepId is not { } stepId
            || !path.TryGetProperty("chainConformance", out var cf) || cf.ValueKind != JsonValueKind.Array) return false;
        var slot = cf.EnumerateArray().FirstOrDefault(c => c.ValueKind == JsonValueKind.Object && SameSlot(c, branchCode, stepId.ToString()));
        return slot.ValueKind == JsonValueKind.Object && Int(slot, "max") is { } max && (Int(slot, "count") ?? 0) >= max;
    }

    private static bool SameSlot(JsonElement conformance, string branchCode, string chainStepId)
        => string.Equals(Str(conformance, "branchCode"), branchCode, StringComparison.OrdinalIgnoreCase)
           && string.Equals(Str(conformance, "chainStepId"), chainStepId, StringComparison.OrdinalIgnoreCase);

    private static bool ArrangedIn(JsonElement item, string branchCode, string chainStepId)
        => item.TryGetProperty("arrangement", out var a) && a.ValueKind == JsonValueKind.Object && SameSlot(a, branchCode, chainStepId);

    private static int ArrangementPosition(JsonElement item)
        => item.TryGetProperty("arrangement", out var a) && a.ValueKind == JsonValueKind.Object ? Int(a, "position") ?? 0 : 0;

    private static string SlotKey(string branchCode, string chainStepId) => $"{branchCode.ToUpperInvariant()}|{chainStepId.ToLowerInvariant()}";

    private static bool SameLanguage(string? a, string? b)
        => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Bindable = published, not archived, branched with every branch coded and stepped (the KP-1 rule).</summary>
    private static bool IsBindable(JsonElement template)
        => string.Equals(Str(template, "status"), "published", StringComparison.OrdinalIgnoreCase)
           && !Bool(template, "isArchived")
           && template.TryGetProperty("branches", out var b) && b.ValueKind == JsonValueKind.Array && b.GetArrayLength() > 0
           && b.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Object && Str(x, "branchCode") is not null
                                          && x.TryGetProperty("steps", out var st) && st.ValueKind == JsonValueKind.Array
                                          && st.GetArrayLength() > 0);

    private sealed record ChainSlot(string Id, int Min, int? Max);

    private sealed record ChainBranch(string Code, string Name, int SortOrder, IReadOnlyList<ChainSlot> Steps);

    /// <summary>Branches in display order (SortOrder, then list order) — the branch-first order KP-1 computes with.</summary>
    private static IReadOnlyList<ChainBranch> Branches(JsonElement template)
        => Items(template, "branches")
            .Where(x => x.ValueKind == JsonValueKind.Object && Str(x, "branchCode") is not null)
            .Select((x, i) => (Branch: new ChainBranch(
                Str(x, "branchCode")!,
                Str(x, "branchName") ?? Str(x, "branchCode")!,
                Int(x, "sortOrder") ?? 0,
                Items(x, "steps")
                    .Where(y => y.ValueKind == JsonValueKind.Object && Str(y, "conceptTypeId") is not null)
                    .Select(y => new ChainSlot(Str(y, "conceptTypeId")!, Int(y, "minSelection") ?? 0, Int(y, "maxSelection")))
                    .ToList()), Index: i))
            .OrderBy(x => x.Branch.SortOrder).ThenBy(x => x.Index).Select(x => x.Branch).ToList();

    private sealed record SubjectInfo(string Name, string? ProductName);

    /// <summary>Subjects with their primary MDM Global Product (SourceSystem global-product, IsPrimary) — the chain's
    /// product rule (KP-1 ChainContextResolver).</summary>
    private async Task<Dictionary<string, SubjectInfo>> SubjectsAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, SubjectInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var subject in await ReadItemsAsync("/api/crm/knowledge/subjects?includeArchived=true", ct) ?? [])
        {
            if (Str(subject, "subjectId") is not { } id) continue;
            string? product = null;
            if (subject.TryGetProperty("externalReferences", out var refs) && refs.ValueKind == JsonValueKind.Array)
            {
                var primary = refs.EnumerateArray().FirstOrDefault(r => r.ValueKind == JsonValueKind.Object
                    && string.Equals(Str(r, "sourceSystem"), GlobalProductSource, StringComparison.OrdinalIgnoreCase)
                    && r.TryGetProperty("isPrimary", out var ip) && ip.ValueKind == JsonValueKind.True);
                if (primary.ValueKind == JsonValueKind.Object) product = Str(primary, "externalName") ?? Str(primary, "externalCode");
            }

            result[id] = new SubjectInfo(Str(subject, "subjectName") ?? Str(subject, "subjectCode") ?? id, product);
        }

        return result;
    }

    private async Task<Dictionary<string, string>> NamesAsync(string path, string idKey, string nameKey, CancellationToken ct)
        => (await ReadItemsAsync(path, ct) ?? [])
            .Where(x => Str(x, idKey) is not null && Str(x, nameKey) is not null)
            .GroupBy(x => Str(x, idKey)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Str(g.First(), nameKey)!, StringComparer.OrdinalIgnoreCase);

    private static List<LocalizedText> LocalizedTexts(JsonElement? version, string property)
        => version is { ValueKind: JsonValueKind.Object } v && v.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object && Str(x, "languageCode") is not null && Str(x, "text") is not null)
                .Select(x => new LocalizedText(Str(x, "languageCode")!, Str(x, "text")!))
                .ToList()
            : [];

    private readonly record struct LocalizedText(string Language, string? Text);

    /// <summary>WP-BRD-TENANT-CRM-SETS — a reference set through the shared <see cref="Diten.Web.Services.CrmReferenceSetReader"/>
    /// (consumable-sets first, so any tenant role reads it). Returns the envelope's <c>data</c>, or null when unavailable.</summary>
    private async Task<JsonElement?> ReadReferenceSetDataAsync(string setCode, CancellationToken ct)
    {
        using var response = await new Diten.Web.Services.CrmReferenceSetReader(_httpClient, _gatewayUrl, _logger).ReadAsync(
            setCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var data)
                ? data.Clone()
                : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Studio reference set read failed: {SetCode}", setCode);
            return null;
        }
    }
    private async Task<JsonElement?> ReadDataAsync(string path, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var d)
                ? d.Clone()
                : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "KnowledgePath studio read failed: {Path}", path);
            return null;
        }
    }

    /// <summary>The items of a list read (<c>data.items</c> or a bare array); null when unavailable.</summary>
    private async Task<List<JsonElement>?> ReadItemsAsync(string path, CancellationToken ct)
    {
        var data = await ReadDataAsync(path, ct);
        if (data is { ValueKind: JsonValueKind.Array } arr) return arr.EnumerateArray().Select(x => x.Clone()).ToList();
        if (data is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array) return items.EnumerateArray().Select(x => x.Clone()).ToList();
        return null;
    }

    private IActionResult Unavailable()
        => StatusCode(StatusCodes.Status502BadGateway, new { errors = new[] { Label("Err_unavailable") } });

    private string Label(string key) => _studioLocalizer[key].Value;

    /// <summary>A resx label, or the generic "other" label when the key is unknown — never the raw code.</summary>
    private string? Labelled(string prefix, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var value = _studioLocalizer[prefix + code.Trim().ToLowerInvariant()];
        return value.ResourceNotFound ? Label(prefix + "other") : value.Value;
    }

    private string? StatusLabel(string? status) => Labelled("Status_", status);

    private string? TypeLabel(string? type) => Labelled("CType_", type);

    private string? ClaimStateLabel(string? state) => Labelled("ClaimState_", state);

    private static string? LanguageName(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : ClaimDisplayNames.LanguageName(code.Trim()) ?? code.Trim();

    /// <summary>The step type a new slot item gets from its content type (the step vocabulary has no "presentation";
    /// the core message is the neutral default) — same mapping as the SB-2 release.</summary>
    internal static string DefaultStepType(string? contentType) => (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "quiz" => "quiz",
        "faq" => "faq",
        "objection-handling" => "objection-handling",
        "clinical-summary" => "clinical-evidence",
        "lesson" or "training-material" => "lesson",
        _ => "core-message"
    };

    private static string? Str(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(p.GetString())
            ? p.GetString()
            : null;

    private static int? Int(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
           && p.TryGetInt32(out var n)
            ? n
            : null;

    private static bool Bool(JsonElement el, string name) => GetBool(el, name);

    private static IEnumerable<JsonElement> Items(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray()
            : [];

    private static IEnumerable<string> Guids(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)
            : [];
}
