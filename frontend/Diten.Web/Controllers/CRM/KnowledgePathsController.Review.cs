using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using R = Diten.Web.Controllers.CRM.KnowledgePathStudioReview;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-UI-2 — the studio's review surface on top of KP-2 / KP-3: the allowlisted proxies (submit / withdraw, revisions,
/// one-channel decision, notes, MOD-0023 history, render, the archive PDF stream, release, withdrawal, usage), the
/// reviewer page (<c>/CRM/KnowledgePaths/{pathId}/Review/{revisionId}</c> — the Work Center link of KP-2's
/// DisplayContext) and the labelled read models of the Compliance / MLR / Output &amp; release / Revisions / Preview /
/// Usage tabs and the legacy-path wizard. Labels only; CRM decides every rule, the person SoD included (the decision
/// panel is hidden from the submitter here and CRM answers 403 anyway).
/// </summary>
public sealed partial class KnowledgePathsController
{
    /// <summary>The CRM (KP-2 / KP-3) review and release codes the studio shows as user text (Err_{code}).</summary>
    public static readonly IReadOnlyList<string> ReviewErrorCodes =
    [
        // KP-2
        "chain_template_required", "review_round_open", "chain_conformance_failed", "component_not_published",
        "component_language_mismatch", "claim_no_country_version", "approval_template_missing", "workflow_unavailable",
        "comment_required", "sod_submitter_cannot_decide", "approval_via_workflow_only",
        // KP-3
        "revision_not_approved", "artifact_missing", "revision_superseded", "revision_not_released",
        "sod_submitter_cannot_release", "reason_required", "path_in_use", "previous_path_in_use",
        "artifact_store_unavailable", "claim_not_approved", "claim_language_mismatch",
        // the shared review answers of the same endpoints
        "approval_forbidden", "no_open_review", "decision_invalid", "note_text_required", "note_not_found"
    ];

    private const string PathsBase = "/api/crm/knowledge/paths";
    private const string DraftRef = "draft";

    // ---------------- reviewer page ----------------

    /// <summary>The reviewer view of one frozen revision (read; UAS-001: without read a plain 403, no skeleton). The
    /// decision panel, notes and evidence come from api/studio/paths/{id}/revisions/{rev}/view.</summary>
    [HttpGet("{pathId:guid}/Review/{revisionId:guid}")]
    public IActionResult Review(Guid pathId, Guid revisionId)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        ViewData["PathId"] = pathId.ToString();
        ViewData["RevisionId"] = revisionId.ToString();
        return View($"{ViewRoot}/Review.cshtml");
    }

    // ---------------- KP-2 proxies ----------------

    [HttpPost("api/paths/{pathId:guid}/submit-review")]
    public Task<IActionResult> SubmitReview(Guid pathId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/submit-review", null, ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/withdraw-review")]
    public Task<IActionResult> WithdrawReview(Guid pathId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/withdraw-review", null, ManagePermission, ct, ManageFallback);

    [HttpGet("api/paths/{pathId:guid}/revisions")]
    public Task<IActionResult> RevisionList(Guid pathId, CancellationToken ct) =>
        ProxyGetAsync($"{PathsBase}/{pathId}/revisions", ReadPermission, ct, ReadFallback);

    [HttpGet("api/paths/{pathId:guid}/revisions/{revisionId:guid}")]
    public Task<IActionResult> RevisionGet(Guid pathId, Guid revisionId, CancellationToken ct) =>
        ProxyGetAsync($"{PathsBase}/{pathId}/revisions/{revisionId}", ReadPermission, ct, ReadFallback);

    // One channel (K1): the same MOD-0023 task the Work Center shows; read is enough here, MOD-0023 decides who may act.
    [HttpPost("api/paths/{pathId:guid}/revisions/{revisionId:guid}/decision")]
    public Task<IActionResult> Decide(Guid pathId, Guid revisionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/revisions/{revisionId}/decision", body, ReadPermission, ct, ReadFallback);

    [HttpPost("api/paths/{pathId:guid}/revisions/{revisionId:guid}/notes")]
    public Task<IActionResult> AddNote(Guid pathId, Guid revisionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/revisions/{revisionId}/notes", body, ReadPermission, ct, ReadFallback);

    [HttpPost("api/paths/{pathId:guid}/revisions/{revisionId:guid}/notes/{noteId:guid}/resolve")]
    public Task<IActionResult> ResolveNote(Guid pathId, Guid revisionId, Guid noteId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/revisions/{revisionId}/notes/{noteId}/resolve", null,
            ReadPermission, ct, ReadFallback);

    [HttpGet("api/paths/{pathId:guid}/review-history")]
    public Task<IActionResult> ReviewHistory(Guid pathId, CancellationToken ct) =>
        ProxyGetAsync($"{PathsBase}/{pathId}/review-history", ReadPermission, ct, ReadFallback);

    // ---------------- KP-3 proxies ----------------

    [HttpPost("api/paths/{pathId:guid}/revisions/{revisionId:guid}/render")]
    public Task<IActionResult> RenderRevision(Guid pathId, Guid revisionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/revisions/{revisionId}/render", null, PublishPermission, ct);

    /// <summary>The archive PDF, streamed (inline for the embedded viewer; <c>download=true</c> as an attachment). The
    /// content id is resolved by CRM from the revision. A failed read passes its status through, bodiless when the
    /// upstream had no body (memory proxy-forward-204-content-length-crash); a "success" without bytes is a 404.</summary>
    [HttpGet("api/paths/{pathId:guid}/revisions/{revisionId:guid}/artifact")]
    public async Task<IActionResult> RevisionArtifact(
        Guid pathId, Guid revisionId, [FromQuery] string? kind, [FromQuery] bool download, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var requested = string.IsNullOrWhiteSpace(kind) ? "pdf" : kind.Trim().ToLowerInvariant();
        if (requested != "pdf")
            return NotFound(new { errors = new[] { "artifact_missing", "Only the archive PDF is rendered in this phase." } });
        var response = await SendGatewayAsync(HttpMethod.Get, $"{PathsBase}/{pathId}/revisions/{revisionId}/artifact?kind=pdf", null, ct);
        return await ToArtifactResultAsync(response, download, ct);
    }

    [HttpPost("api/paths/{pathId:guid}/revisions/{revisionId:guid}/release")]
    public Task<IActionResult> ReleaseRevision(Guid pathId, Guid revisionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/revisions/{revisionId}/release", null, PublishPermission, ct);

    [HttpPost("api/paths/{pathId:guid}/revisions/{revisionId:guid}/withdraw")]
    public Task<IActionResult> WithdrawRelease(Guid pathId, Guid revisionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PathsBase}/{pathId}/revisions/{revisionId}/withdraw", body, PublishPermission, ct);

    [HttpGet("api/paths/{pathId:guid}/usage")]
    public Task<IActionResult> PathUsage(Guid pathId, CancellationToken ct) =>
        ProxyGetAsync($"{PathsBase}/{pathId}/usage", ReadPermission, ct, ReadFallback);

    internal static async Task<IActionResult> ToArtifactResultAsync(HttpResponseMessage? response, bool download, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        var status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (IsBodiless(status) || string.IsNullOrWhiteSpace(body)) return new StatusCodeResult(status);
            return new ContentResult
            {
                StatusCode = status,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = body
            };
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (IsBodiless(status) || bytes.Length == 0)
            return new NotFoundObjectResult(new { errors = new[] { "artifact_missing", "The revision has no rendered output." } });

        var media = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        var file = new FileContentResult(bytes, media);
        if (download)
        {
            var disposition = response.Content.Headers.ContentDisposition;
            file.FileDownloadName = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"') ?? "knowledge-path.pdf";
        }

        return file;
    }

    private static bool IsBodiless(int status) => status is 204 or 205 or 304;

    // ---------------- studio review model (MLR / output & release / revisions / preview tabs + next step) ----------------

    /// <summary>Everything the review tabs show, labelled: the revisions (status, submitter, MLR decisions with step
    /// NAME + person + time + comment, the archive PDF, the release), the MLR timeline of the latest revision (pending
    /// step + candidate positions from the KP-MLR template), the three state cards, the submit / withdraw state, the
    /// release preconditions, the "next step" band and which tabs have something to show.</summary>
    [HttpGet("api/studio/paths/{pathId:guid}/review")]
    public async Task<IActionResult> StudioReview(Guid pathId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var detail = await ReadDataAsync($"{PathsBase}/{pathId}", ct);
        if (detail is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });

        var actor = R.ActorOf(User);
        var canManage = HasAnyPermission(ManagePermission, ManageFallback);
        var canPublish = HasAnyPermission(PublishPermission);
        var status = Str(p, "pathStatus");
        var legacy = !p.TryGetProperty("isLegacyUnapproved", out var l) || l.ValueKind != JsonValueKind.False;
        var archived = Bool(p, "isArchived");

        var revisions = await RevisionDetailsAsync(pathId, ct);
        var histories = await HistoriesAsync(pathId, ct);
        var person = PersonLabeller(actor, histories.Values);
        JsonElement? latest = revisions.Count > 0 ? revisions[0] : null;
        var latestOpen = latest is { } lr && RoundOpen(lr);
        var plan = latest is { } lp ? await WorkflowPlanAsync(Round(lp, "templateCode"), ct) : null;
        var timeline = latest is { } lt
            ? R.Timeline(plan, R.ReadHistory(histories.GetValueOrDefault(Str(lt, "revisionId") ?? string.Empty)), latestOpen, Label, person)
            : [];
        var pending = timeline.FirstOrDefault(s => s.State == R.StepStates.Pending);
        var compliance = legacy ? [] : await ComplianceForAsync(p, ct);
        var blockers = compliance.Count(r => r.Severity == R.Blocker);

        // The release candidate: the newest approved revision not released yet; the live one: released, not withdrawn.
        var candidate = revisions.Cast<JsonElement?>().FirstOrDefault(r => Same(Str(r!.Value, "status"), "approved") && ReleaseState(r!.Value) is null);
        var live = revisions.Cast<JsonElement?>().FirstOrDefault(r => Same(ReleaseState(r!.Value), "released"));
        var liveVersion = await LiveVersionAsync(p, ct);

        object? candidateModel = null;
        if (candidate is { } cr)
        {
            var artifact = Pdf(cr);
            var isSubmitter = R.IsSubmitter(actor, cr);
            var approvedNow = Same(status, "approved") && Same(Str(cr, "revisionId"), revisions.Where(r => Same(Str(r, "status"), "approved")).Select(r => Str(r, "revisionId")).FirstOrDefault());
            var preconditions = R.ReleasePreconditions(approvedNow, artifact is not null, isSubmitter, compliance,
                Int(cr, "revisionNumber") ?? 0, Label);
            candidateModel = new
            {
                revisionId = Str(cr, "revisionId"),
                revisionNumber = Int(cr, "revisionNumber"),
                pathVersion = Str(cr, "pathVersion"),
                artifact = ArtifactModel(pathId, cr, artifact),
                preconditions = preconditions.Select(x => new { key = x.Key, label = x.Label, ok = x.Ok, reason = x.Reason }).ToList(),
                canRender = canPublish && approvedNow && artifact is null,
                canRelease = canPublish && preconditions.All(x => x.Ok),
                replacesVersion = liveVersion
            };
        }

        object? liveModel = live is { } lv
            ? new
            {
                revisionId = Str(lv, "revisionId"),
                revisionNumber = Int(lv, "revisionNumber"),
                pathVersion = Str(lv, "pathVersion"),
                releasedAt = Release(lv, "at"),
                releasedBy = person(Release(lv, "by"), null),
                artifact = ArtifactModel(pathId, lv, Pdf(lv)),
                canWithdraw = canPublish && Same(status, "published")
            }
            : null;

        var rows = revisions.Select(r =>
        {
            var id = Str(r, "revisionId") ?? string.Empty;
            var open = RoundOpen(r);
            var steps = R.Timeline(latest is { } lx && Same(id, Str(lx, "revisionId")) ? plan : null,
                R.ReadHistory(histories.GetValueOrDefault(id)), open, Label, person);
            var release = ReleaseState(r);
            return new
            {
                revisionId = id,
                revisionNumber = Int(r, "revisionNumber"),
                pathVersion = Str(r, "pathVersion"),
                status = Str(r, "status"),
                statusLabel = Labelled("RevStatus_", Str(r, "status")),
                submittedAt = Round(r, "submittedAt") ?? Str(r, "createdAt"),
                submittedBy = person(Round(r, "submittedBy") ?? Str(r, "submittedBy"), null),
                isMine = R.IsSubmitter(actor, r),
                isOpen = open,
                decisions = steps.Where(s => s.State is R.StepStates.Approved or R.StepStates.Rejected or R.StepStates.Cancelled)
                    .Select(s => new { stepName = s.StepName, state = s.State, stateLabel = s.StateLabel, actor = s.Actor, at = s.At, comment = s.Comment })
                    .ToList(),
                artifact = ArtifactModel(pathId, r, Pdf(r)),
                release = release is null ? null : new
                {
                    state = release,
                    stateLabel = Labelled("Release_", release),
                    at = Release(r, "at"),
                    by = person(Release(r, "by"), null),
                    reason = Release(r, "reason")
                },
                isLive = Same(release, "released") && Same(status, "published"),
                openNoteCount = Items(r, "notes").Count(n => !Bool(n, "isResolved")),
                changeCount = r.TryGetProperty("changeSummary", out var cs) ? Items(cs, "items").Count() : 0,
                comparedTo = r.TryGetProperty("changeSummary", out var cs2) ? Int(cs2, "comparedToRevision") : null
            };
        }).ToList();

        var version = Str(p, "pathVersion") ?? string.Empty;
        var cards = new
        {
            field = live is { } f && Same(status, "published")
                ? R.Format(Label("CardRev"), Str(f, "pathVersion"), Int(f, "revisionNumber"))
                : liveVersion is null ? null : R.Format(Label("CardVersion"), liveVersion),
            approval = latest is { } a && (latestOpen || (Same(Str(a, "status"), "approved") && ReleaseState(a) is null))
                ? R.Format(Label("CardRev"), Str(a, "pathVersion"), Int(a, "revisionNumber"))
                : null,
            working = Same(status, "draft") ? R.Format(Label("CardDraft"), version) : null
        };

        var canSubmit = canManage && !legacy && !archived && Same(status, "draft") && !latestOpen;
        var next = NextStep(legacy, archived, status, blockers, latestOpen, pending?.StepName, candidate, canPublish,
            candidateModel is null ? null : Pdf(candidate!.Value));

        return Ok(new
        {
            data = new
            {
                pathId = Str(p, "pathId"),
                status,
                statusLabel = StatusLabel(status),
                statusDetail = latestOpen && pending is not null ? R.Format(Label("StatusInReviewStep"), pending.StepName) : null,
                liveVersion,
                canManage,
                canPublish,
                submit = new
                {
                    canSubmit,
                    blockers,
                    nextRevisionNumber = (latest is { } n ? Int(n, "revisionNumber") ?? 0 : 0) + 1,
                    lastRevisionId = latest is { } x ? Str(x, "revisionId") : null,
                    lastRevisionNumber = latest is { } y ? Int(y, "revisionNumber") : null,
                    openRound = latestOpen
                        ? new { revisionNumber = Int(latest!.Value, "revisionNumber"), stepName = pending?.StepName }
                        : null,
                    canWithdrawReview = latestOpen && (canManage || R.IsSubmitter(actor, latest!.Value))
                },
                timeline = latest is null ? null : new
                {
                    revisionId = Str(latest.Value, "revisionId"),
                    revisionNumber = Int(latest.Value, "revisionNumber"),
                    pathVersion = Str(latest.Value, "pathVersion"),
                    submittedAt = Round(latest.Value, "submittedAt"),
                    submittedBy = person(Round(latest.Value, "submittedBy") ?? Str(latest.Value, "submittedBy"), null),
                    isOpen = latestOpen,
                    available = histories.ContainsKey(Str(latest.Value, "revisionId") ?? string.Empty),
                    steps = timeline.Select(s => new
                    {
                        stepName = s.StepName, state = s.State, stateLabel = s.StateLabel, actor = s.Actor, at = s.At,
                        comment = s.Comment, candidates = s.Candidates
                    }).ToList()
                },
                revisions = rows,
                cards,
                release = new { candidate = candidateModel, live = liveModel },
                next,
                tabs = new
                {
                    compliance = !legacy,
                    mlr = revisions.Count > 0,
                    output = canPublish && (candidateModel is not null || liveModel is not null),
                    revisions = revisions.Count > 0,
                    preview = !legacy,
                    usage = true
                }
            }
        });
    }

    /// <summary>The "next step" band (mockup: fix → submit → wait → render → release).</summary>
    private object? NextStep(bool legacy, bool archived, string? status, int blockers, bool roundOpen, string? pendingStep,
        JsonElement? candidate, bool canPublish, JsonElement? candidatePdf)
    {
        object Band(string key, string text, string? action, string? actionLabel) => new { key, text, action, actionLabel };
        if (archived) return null;
        if (legacy) return Band("bind", Label("NextBind"), "wizard", Label("WizardOpen"));
        if (roundOpen) return Band("waiting", R.Format(Label("NextWaiting"), pendingStep ?? string.Empty), "mlr", Label("TabMlr"));
        if (candidate is not null)
        {
            if (!canPublish) return Band("publisher", Label("NextPublisher"), null, null);
            return candidatePdf is null
                ? Band("render", Label("NextRender"), "output", Label("TabOutput"))
                : Band("release", Label("NextRelease"), "output", Label("TabOutput"));
        }

        if (Same(status, "published")) return Band("live", Label("NextLive"), null, null);
        if (!Same(status, "draft")) return null;
        return blockers > 0
            ? Band("fix", R.Format(Label("NextFix"), blockers), "compliance", Label("ShowBlockers"))
            : Band("submit", Label("NextSubmit"), "submit", Label("SubmitOpen"));
    }

    // ---------------- reviewer view model ----------------

    /// <summary>The frozen revision as the reviewer sees it: its chain branches / steps with the contents (title +
    /// version) and claim blocks (the path-language text + qualifier of the pinned country version), note counts per
    /// step / block, the note stream (author, time, resolved, carried from an earlier revision), the step being decided
    /// and whether THIS user may decide — never the submitter (person-based).</summary>
    [HttpGet("api/studio/paths/{pathId:guid}/revisions/{revisionId:guid}/view")]
    public async Task<IActionResult> ReviewerView(Guid pathId, Guid revisionId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var path = await ReadDataAsync($"{PathsBase}/{pathId}", ct);
        var revision = await ReadDataAsync($"{PathsBase}/{pathId}/revisions/{revisionId}", ct);
        if (path is not { ValueKind: JsonValueKind.Object } p || revision is not { ValueKind: JsonValueKind.Object } r)
            return NotFound(new { errors = new[] { "Revision not found." } });

        var actor = R.ActorOf(User);
        var canManage = HasAnyPermission(ManagePermission, ManageFallback);
        var histories = await HistoriesAsync(pathId, ct);
        var person = PersonLabeller(actor, histories.Values);
        var open = RoundOpen(r);
        var plan = await WorkflowPlanAsync(Round(r, "templateCode"), ct);
        var timeline = R.Timeline(plan, R.ReadHistory(histories.GetValueOrDefault(revisionId.ToString())), open, Label, person);
        var pending = timeline.FirstOrDefault(s => s.State == R.StepStates.Pending);
        var isSubmitter = R.IsSubmitter(actor, r);

        var snapshot = r.TryGetProperty("snapshot", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
        var language = Str(snapshot, "languageCode");
        var country = Str(snapshot, "countryCode");
        var slots = await SlotNamesAsync(Str(snapshot, "conceptChainTemplateId"), ct);
        var notes = Items(r, "notes").Where(n => n.ValueKind == JsonValueKind.Object).ToList();
        int NotesOn(string field, string reference) => notes.Count(n => !Bool(n, "isResolved") && Same(Str(n, field), reference));

        var steps = Items(snapshot, "steps").Where(x => x.ValueKind == JsonValueKind.Object).ToList();
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contentId in steps.Select(x => Str(x, "contentId")).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await ReadDataAsync($"/api/crm/knowledge/contents/{contentId}", ct) is { ValueKind: JsonValueKind.Object } c
                && Str(c, "contentTitle") is { } title) titles[contentId] = title;
        }

        var claimBlocks = new List<(JsonElement Claim, object Block)>();
        foreach (var claim in Items(snapshot, "claims").Where(x => x.ValueKind == JsonValueKind.Object))
        {
            var versionId = Str(claim, "countryVersionId");
            var version = versionId is null ? null : await ReadDataAsync($"/api/crm/content-composition/claims/country-versions/{versionId}", ct);
            var core = Str(claim, "claimId") is { } claimId ? await ReadDataAsync($"/api/crm/content-composition/claims/{claimId}", ct) : null;
            var blockRef = $"claim:{Str(claim, "claimId")}";
            claimBlocks.Add((claim, new
            {
                kind = "claim",
                blockRef,
                claimId = Str(claim, "claimId"),
                countryVersionId = versionId,
                code = Str(claim, "claimCode"),
                name = core is { } cc ? Str(cc, "claimName") ?? Str(cc, "name") : null,
                text = LocalizedTexts(version, "texts").FirstOrDefault(t => SameLanguage(t.Language, language)).Text,
                qualifier = LocalizedTexts(version, "qualifiers").FirstOrDefault(t => SameLanguage(t.Language, language)).Text,
                versionLabel = Str(claim, "countryVersion") is { } cv ? R.Format(Label("ClaimVersionLabel"), cv) : null,
                statusLabel = Str(claim, "countryVersionStatus") is { } st ? ClaimStateLabel(st) : null,
                position = ArrangementPosition(claim),
                noteCount = NotesOn("blockRef", blockRef)
            }));
        }

        var branches = slots.GroupBy(x => (x.BranchCode, x.BranchName)).Select(g => new
        {
            code = g.Key.BranchCode,
            name = g.Key.BranchName,
            slots = g.Select(slot =>
            {
                var stepRef = $"{slot.BranchCode}:{slot.ChainStepId}";
                var items = steps.Where(x => ArrangedIn(x, slot.BranchCode, slot.ChainStepId))
                    .Select(x =>
                    {
                        var blockRef = $"content:{Str(x, "stepId")}";
                        return (Position: ArrangementPosition(x), Kind: 0, Item: (object)new
                        {
                            kind = "content",
                            blockRef,
                            title = Str(x, "contentId") is { } cid && titles.TryGetValue(cid, out var t) ? t : Str(x, "stepTitle"),
                            code = Str(x, "contentCode"),
                            versionLabel = Str(x, "contentVersion") is { } v ? R.Format(Label("ContentVersionLabel"), v) : null,
                            isRequired = Bool(x, "isRequired"),
                            noteCount = NotesOn("blockRef", blockRef)
                        });
                    })
                    .Concat(claimBlocks.Where(c => ArrangedIn(c.Claim, slot.BranchCode, slot.ChainStepId))
                        .Select(c => (Position: ArrangementPosition(c.Claim), Kind: 1, Item: c.Block)))
                    .OrderBy(x => x.Position).ThenBy(x => x.Kind).Select(x => x.Item).ToList();
                return new { chainStepId = slot.ChainStepId, name = slot.StepName, stepRef, noteCount = NotesOn("stepRef", stepRef), items };
            }).ToList()
        }).ToList();

        string? TargetOf(JsonElement note)
        {
            var stepRef = Str(note, "stepRef");
            var slot = stepRef is null ? null : slots.FirstOrDefault(x => Same($"{x.BranchCode}:{x.ChainStepId}", stepRef));
            return slot is null ? null : $"{slot.BranchName} › {slot.StepName}";
        }

        var roundSubmitter = Round(r, "submittedBy") ?? Str(r, "submittedBy");
        var decisionNote = isSubmitter ? Label("DecisionOwnRevision")
            : !open ? Label("DecisionClosed")
            : null;
        return Ok(new
        {
            data = new
            {
                pathId = Str(p, "pathId"),
                revisionId = Str(r, "revisionId"),
                pathName = Str(snapshot, "pathName") ?? Str(p, "pathName"),
                pathCode = Str(r, "pathCode"),
                pathVersion = Str(r, "pathVersion"),
                revisionNumber = Int(r, "revisionNumber"),
                status = Str(r, "status"),
                statusLabel = Labelled("RevStatus_", Str(r, "status")),
                submittedBy = person(roundSubmitter, null),
                submittedAt = Round(r, "submittedAt") ?? Str(r, "createdAt"),
                context = new
                {
                    countryName = country is null ? null : ClaimDisplayNames.CountryName(country.ToUpperInvariant()) ?? country,
                    languageName = LanguageName(language),
                    productName = Str(snapshot, "productName") ?? Str(snapshot, "productCode")
                },
                roundOpen = open,
                isSubmitter,
                // Person-based SoD: the submitter never gets the decision panel; MOD-0023 decides candidacy (403 → text).
                canDecide = open && !isSubmitter && actor is not null,
                currentStepName = pending?.StepName,
                decisionNote,
                canManage,
                branches,
                notes = notes.OrderBy(n => Str(n, "createdAt")).Select(n => new
                {
                    noteId = Str(n, "noteId"),
                    stepRef = Str(n, "stepRef"),
                    blockRef = Str(n, "blockRef"),
                    target = TargetOf(n),
                    text = Str(n, "text"),
                    author = person(Str(n, "author"), null),
                    createdAt = Str(n, "createdAt"),
                    isResolved = Bool(n, "isResolved"),
                    resolvedBy = Str(n, "resolvedBy") is { } rb ? person(rb, null) : null,
                    carriedLabel = Int(n, "carriedFromRevision") is { } from ? R.Format(Label("NoteCarried"), from) : null,
                    canResolve = !Bool(n, "isResolved") && (canManage || R.SamePerson(actor, Str(n, "author")))
                }).ToList()
            }
        });
    }

    // ---------------- revision difference ----------------

    /// <summary>The difference between two revisions, or a revision and the current draft (<c>to=draft</c>): steps,
    /// contents (+ version) and claims (+ country version) by step / claim code, with the chain step NAMES.</summary>
    [HttpGet("api/studio/paths/{pathId:guid}/diff")]
    public async Task<IActionResult> RevisionDiff(Guid pathId, [FromQuery] Guid from, [FromQuery] string? to, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var path = await ReadDataAsync($"{PathsBase}/{pathId}", ct);
        var before = await ReadDataAsync($"{PathsBase}/{pathId}/revisions/{from}", ct);
        if (path is not { ValueKind: JsonValueKind.Object } p || before is not { ValueKind: JsonValueKind.Object } b)
            return NotFound(new { errors = new[] { "Revision not found." } });

        var chainId = p.TryGetProperty("chainTemplate", out var chain) && chain.ValueKind == JsonValueKind.Object ? Str(chain, "id") : null;
        var slots = await SlotNamesAsync(chainId, ct);
        string? SlotLabel(JsonElement item)
        {
            if (!item.TryGetProperty("arrangement", out var a) || a.ValueKind != JsonValueKind.Object) return null;
            var slot = slots.FirstOrDefault(x => Same(x.BranchCode, Str(a, "branchCode")) && Same(x.ChainStepId, Str(a, "chainStepId")));
            return slot is null ? null : $"{slot.BranchName} › {slot.StepName}";
        }

        (List<R.DiffItem> Steps, List<R.DiffItem> Claims) FromSnapshot(JsonElement revision)
        {
            var snap = revision.TryGetProperty("snapshot", out var sn) ? sn : default;
            return (Items(snap, "steps").Where(x => x.ValueKind == JsonValueKind.Object)
                    .Select(x => new R.DiffItem(Str(x, "stepCode") ?? Str(x, "stepId") ?? string.Empty, Str(x, "stepTitle") ?? string.Empty,
                        SlotLabel(x), ArrangementPosition(x), Str(x, "contentVersion"), Str(x, "contentCode"))).ToList(),
                Items(snap, "claims").Where(x => x.ValueKind == JsonValueKind.Object)
                    .Select(x => new R.DiffItem(Str(x, "claimCode") ?? string.Empty, Str(x, "claimCode") ?? string.Empty,
                        SlotLabel(x), ArrangementPosition(x), Str(x, "countryVersion"), null)).ToList());
        }

        var left = FromSnapshot(b);
        (List<R.DiffItem> Steps, List<R.DiffItem> Claims) right;
        string toLabel;
        if (string.IsNullOrWhiteSpace(to) || Same(to, DraftRef))
        {
            var steps = new List<R.DiffItem>();
            foreach (var x in Items(p, "steps").Where(x => x.ValueKind == JsonValueKind.Object && !Bool(x, "isArchived")))
            {
                var content = Str(x, "contentId") is { } cid ? await ReadDataAsync($"/api/crm/knowledge/contents/{cid}", ct) : null;
                steps.Add(new R.DiffItem(Str(x, "stepCode") ?? Str(x, "stepId") ?? string.Empty, Str(x, "stepTitle") ?? string.Empty,
                    SlotLabel(x), ArrangementPosition(x), content is { } c1 ? Str(c1, "contentVersion") : null,
                    content is { } c2 ? Str(c2, "contentCode") : null));
            }

            right = (steps, Items(p, "claims").Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(x => new R.DiffItem(Str(x, "claimCode") ?? string.Empty, Str(x, "claimCode") ?? string.Empty,
                    SlotLabel(x), ArrangementPosition(x), Str(x, "countryVersion"), null)).ToList());
            toLabel = R.Format(Label("CardDraft"), Str(p, "pathVersion"));
        }
        else
        {
            if (!Guid.TryParse(to, out var toId) || await ReadDataAsync($"{PathsBase}/{pathId}/revisions/{toId}", ct) is not { ValueKind: JsonValueKind.Object } after)
                return NotFound(new { errors = new[] { "Revision not found." } });
            right = FromSnapshot(after);
            toLabel = R.Format(Label("CardRev"), Str(after, "pathVersion"), Int(after, "revisionNumber"));
        }

        var rows = R.Diff(left.Steps, right.Steps, left.Claims, right.Claims, Label);
        return Ok(new
        {
            data = new
            {
                fromLabel = R.Format(Label("CardRev"), Str(b, "pathVersion"), Int(b, "revisionNumber")),
                toLabel,
                rows = rows.Select(x => new { kind = x.Kind, kindLabel = x.KindLabel, label = x.Label, from = x.From, to = x.To }).ToList()
            }
        });
    }

    // ---------------- usage ----------------

    /// <summary>Where the path is used: journey stages (journey + stage NAME, status label, pin policy as text) and
    /// strategy templates, each with a link. Page-view data is a later phase (the tab says so).</summary>
    [HttpGet("api/studio/paths/{pathId:guid}/usage")]
    public async Task<IActionResult> StudioUsage(Guid pathId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var usage = await ReadDataAsync($"{PathsBase}/{pathId}/usage", ct);
        if (usage is not { ValueKind: JsonValueKind.Object } u) return Unavailable();
        return Ok(new
        {
            data = new
            {
                journeys = Items(u, "journeys").Where(j => j.ValueKind == JsonValueKind.Object).Select(j => new
                {
                    name = Str(j, "name") ?? Str(j, "code"),
                    code = Str(j, "code"),
                    stageName = Str(j, "stageName"),
                    statusLabel = StatusLabel(Str(j, "status")),
                    pinLabel = Same(Str(j, "pinPolicy"), "latest-published")
                        ? Label("PinLatest")
                        : R.Format(Label("PinVersion"), Str(j, "pinnedVersion") ?? string.Empty),
                    url = $"/CRM/ContentEngagementJourneys/Details/{Str(j, "journeyId")}"
                }).ToList(),
                strategyTemplates = Items(u, "strategyTemplates").Where(t => t.ValueKind == JsonValueKind.Object).Select(t => new
                {
                    name = Str(t, "name") ?? Str(t, "code"),
                    code = Str(t, "code"),
                    statusLabel = StatusLabel(Str(t, "status")),
                    url = $"/CRM/StrategyTemplates/Details/{Str(t, "templateId")}"
                }).ToList()
            }
        });
    }

    // ---------------- legacy wizard (K4) ----------------

    /// <summary>Wizard step 2: the path's steps (title, content, current placement, the full body the step update needs)
    /// and the chain's branches / steps by NAME.</summary>
    [HttpGet("api/studio/paths/{pathId:guid}/mapping")]
    public async Task<IActionResult> WizardMapping(Guid pathId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var path = await ReadDataAsync($"{PathsBase}/{pathId}", ct);
        if (path is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });
        var chainId = p.TryGetProperty("chainTemplate", out var chain) && chain.ValueKind == JsonValueKind.Object ? Str(chain, "id") : null;
        var slots = await SlotNamesAsync(chainId, ct);
        var steps = Items(p, "steps").Where(x => x.ValueKind == JsonValueKind.Object && !Bool(x, "isArchived"))
            .OrderBy(x => Int(x, "stepOrder") ?? int.MaxValue).ToList();
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contentId in steps.Select(x => Str(x, "contentId")).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await ReadDataAsync($"/api/crm/knowledge/contents/{contentId}", ct) is { ValueKind: JsonValueKind.Object } c
                && Str(c, "contentTitle") is { } title) titles[contentId] = title;
        }

        return Ok(new
        {
            data = new
            {
                bound = chainId is not null,
                branches = slots.GroupBy(x => (x.BranchCode, x.BranchName)).Select(g => new
                {
                    code = g.Key.BranchCode,
                    name = g.Key.BranchName,
                    slots = g.Select(x => new { chainStepId = x.ChainStepId, name = x.StepName }).ToList()
                }).ToList(),
                steps = steps.Select(x =>
                {
                    var arrangement = x.TryGetProperty("arrangement", out var a) && a.ValueKind == JsonValueKind.Object ? a : (JsonElement?)null;
                    return new
                    {
                        stepId = Str(x, "stepId"),
                        title = Str(x, "stepTitle"),
                        contentTitle = Str(x, "contentId") is { } cid && titles.TryGetValue(cid, out var t) ? t : null,
                        branchCode = arrangement is { } aa ? Str(aa, "branchCode") : null,
                        chainStepId = arrangement is { } ab ? Str(ab, "chainStepId") : null,
                        body = new
                        {
                            stepOrder = Int(x, "stepOrder") ?? 10,
                            stepCode = Str(x, "stepCode"),
                            stepTitle = Str(x, "stepTitle"),
                            stepType = Str(x, "stepType"),
                            contentId = Str(x, "contentId"),
                            isRequired = Bool(x, "isRequired"),
                            versionPinPolicy = Str(x, "versionPinPolicy"),
                            completionRule = Str(x, "completionRule"),
                            prerequisiteStepId = Str(x, "prerequisiteStepId"),
                            conceptNodeId = Str(x, "conceptNodeId"),
                            estimatedDurationMinutes = Int(x, "estimatedDurationMinutes"),
                            notes = Str(x, "notes"),
                            branchConditions = Items(x, "branchConditions").Select(bc => new
                            {
                                conditionCode = Str(bc, "conditionCode"), description = Str(bc, "description"), targetStepId = Str(bc, "targetStepId")
                            }).ToList()
                        }
                    };
                }).ToList()
            }
        });
    }

    /// <summary>Wizard step 3: claim SUGGESTIONS — the claims the path's step contents reference (Knowledge content
    /// ClaimRefs), each with the path-country text / usability (the path-claims lookup) and the step it would go on.
    /// Nothing is bound here: every suggestion is accepted or rejected by the user (K4 / D-KP-6).</summary>
    [HttpGet("api/studio/paths/{pathId:guid}/claim-suggestions")]
    public async Task<IActionResult> ClaimSuggestions(Guid pathId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var path = await ReadDataAsync($"{PathsBase}/{pathId}", ct);
        if (path is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });

        var wanted = new Dictionary<string, (string StepTitle, JsonElement? Arrangement)>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in Items(p, "steps").Where(x => x.ValueKind == JsonValueKind.Object && !Bool(x, "isArchived"))
                     .OrderBy(x => Int(x, "stepOrder") ?? int.MaxValue))
        {
            if (Str(step, "contentId") is not { } contentId
                || await ReadDataAsync($"/api/crm/knowledge/contents/{contentId}", ct) is not { ValueKind: JsonValueKind.Object } content) continue;
            var arrangement = step.TryGetProperty("arrangement", out var a) && a.ValueKind == JsonValueKind.Object ? a.Clone() : (JsonElement?)null;
            foreach (var reference in Items(content, "claimRefs"))
            {
                if (Str(reference, "claimId") is { } claimId)
                    wanted.TryAdd(claimId, (Str(step, "stepTitle") ?? Str(content, "contentTitle") ?? string.Empty, arrangement));
            }
        }

        var lookup = await PathClaimLookup(pathId, null, ct);
        var json = lookup is JsonResult { Value: { } value } ? JsonSerializer.SerializeToElement(value) : default;
        var options = Items(json, "options").Where(o => Str(o, "claimId") is { } id && wanted.ContainsKey(id)).ToList();
        return Ok(new
        {
            data = new
            {
                disabled = json.ValueKind == JsonValueKind.Object && Bool(json, "disabled"),
                reasonLabel = json.ValueKind == JsonValueKind.Object && Str(json, "reason") is { } reason ? Label(reason) : null,
                suggestions = options.Select(o =>
                {
                    var (stepTitle, arrangement) = wanted[Str(o, "claimId")!];
                    return new
                    {
                        claimId = Str(o, "claimId"),
                        code = Str(o, "code"),
                        name = Str(o, "name"),
                        text = Str(o, "text"),
                        qualifier = Str(o, "qualifier"),
                        usable = Bool(o, "usable"),
                        reasonLabel = Str(o, "reasonLabel"),
                        versionLabel = Str(o, "countryVersion") is { } v ? R.Format(Label("ClaimVersionLabel"), v) : null,
                        onPath = Bool(o, "onPath"),
                        fromStep = stepTitle,
                        branchCode = arrangement is { } aa ? Str(aa, "branchCode") : null,
                        chainStepId = arrangement is { } ab ? Str(ab, "chainStepId") : null
                    };
                }).ToList()
            }
        });
    }

    // ---------------- helpers ----------------

    /// <summary>The compliance rows of a chain-bound path, loading its contents and chain step names.</summary>
    private async Task<IReadOnlyList<R.ComplianceRow>> ComplianceForAsync(JsonElement path, CancellationToken ct)
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var contentId in Items(path, "steps").Where(x => !Bool(x, "isArchived")).Select(x => Str(x, "contentId"))
                     .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await ReadDataAsync($"/api/crm/knowledge/contents/{contentId}", ct) is { ValueKind: JsonValueKind.Object } c) contents[contentId] = c;
        }

        var chainId = path.TryGetProperty("chainTemplate", out var chain) && chain.ValueKind == JsonValueKind.Object ? Str(chain, "id") : null;
        return R.Compliance(path, contents, await SlotNamesAsync(chainId, ct), Label, LanguageName, StatusLabel);
    }

    /// <summary>The chain's branches and steps in branch-first order, with their NAMES (branch name, concept type).</summary>
    private async Task<IReadOnlyList<R.SlotName>> SlotNamesAsync(string? chainTemplateId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chainTemplateId)) return [];
        var template = await ReadDataAsync($"/api/crm/knowledge/concept-chain-templates/{chainTemplateId}", ct);
        if (template is not { ValueKind: JsonValueKind.Object } tpl) return [];
        var types = Str(tpl, "subjectId") is { } subjectId
            ? await NamesAsync($"/api/crm/knowledge/concept-types?subjectId={subjectId}&includeArchived=true", "conceptTypeId", "conceptTypeName", ct)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return Branches(tpl).SelectMany(b => b.Steps.Select(st =>
            new R.SlotName(b.Code, b.Name, st.Id, types.GetValueOrDefault(st.Id) ?? Label("MlrUnnamedStep")))).ToList();
    }


    /// <summary>Revision details, newest first (at most 20 — the history a reviewer scrolls).</summary>
    private async Task<List<JsonElement>> RevisionDetailsAsync(Guid pathId, CancellationToken ct)
    {
        var summaries = await ReadItemsAsync($"{PathsBase}/{pathId}/revisions", ct) ?? [];
        var list = new List<JsonElement>();
        foreach (var summary in summaries.OrderByDescending(x => Int(x, "revisionNumber") ?? 0).Take(20))
        {
            if (Str(summary, "revisionId") is { } id
                && await ReadDataAsync($"{PathsBase}/{pathId}/revisions/{id}", ct) is { ValueKind: JsonValueKind.Object } d) list.Add(d);
        }

        return list;
    }

    /// <summary>MOD-0023 history per revision id (only the readable ones — an unavailable history is never invented).</summary>
    private async Task<Dictionary<string, JsonElement>> HistoriesAsync(Guid pathId, CancellationToken ct)
        => (await ReadItemsAsync($"{PathsBase}/{pathId}/review-history", ct) ?? [])
            .Where(h => Str(h, "revisionId") is not null
                        && (!h.TryGetProperty("available", out var a) || a.ValueKind != JsonValueKind.False))
            .GroupBy(h => Str(h, "revisionId")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>A person as text: "You" for the caller, the MOD-0023 display name when history knows it, otherwise a
    /// neutral label — an id is never shown as a name.</summary>
    private Func<string?, string?, string> PersonLabeller(string? actor, IEnumerable<JsonElement> histories)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in histories.SelectMany(h => Items(h, "entries")))
        {
            if (Str(entry, "actorId") is { } id && Str(entry, "actorDisplay") is { } display) names.TryAdd(id, display);
        }

        return (id, display) => R.SamePerson(id, actor) ? Label("PersonYou")
            : display ?? (id is not null && names.TryGetValue(id, out var n) ? n : null) ?? Label("PersonOther");
    }

    /// <summary>The KP-MLR template's steps in order with their candidate position NAMES (Platform reads on the
    /// caller's token); null when the template cannot be read — the timeline then follows the history alone.</summary>
    private async Task<IReadOnlyList<R.PlanStep>?> WorkflowPlanAsync(string? templateCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(templateCode)) return null;
        var definitions = await ReadPlatformAsync("/api/v1/workflow/definitions", ct);
        if (definitions is not { ValueKind: JsonValueKind.Array } list) return null;
        var definition = list.EnumerateArray().FirstOrDefault(d => Same(Str(d, "templateCode"), templateCode));
        if (Str(definition, "id") is not { } definitionId) return null;
        var detail = await ReadPlatformAsync($"/api/v1/workflow/definitions/{definitionId}", ct);
        if (detail is not { } dd || Str(dd, "activePublishedVersionId") is not { } versionId) return null;
        var version = await ReadPlatformAsync($"/api/v1/workflow/definitions/{definitionId}/versions/{versionId}", ct);
        if (version is not { } vv || Str(vv, "definitionJson") is not { } json) return null;

        var positions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (await ReadPlatformAsync("/api/v1/workflow/lookups/positions", ct) is { ValueKind: JsonValueKind.Array } rows)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (Str(row, "id") is { } id) positions[id] = Str(row, "name") ?? Str(row, "code") ?? id;
            }
        }

        try
        {
            using var plan = JsonDocument.Parse(json);
            return Items(plan.RootElement, "stages").SelectMany(stage => Items(stage, "steps")).Where(step => Str(step, "code") is not null)
                .Select(step => new R.PlanStep(Str(step, "code")!, Str(step, "name"),
                    step.TryGetProperty("assignment", out var assignment) && assignment.ValueKind == JsonValueKind.Object
                        ? Items(assignment, "candidatePrincipalIds").Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => x.GetString()!)
                            .Select(raw => raw.StartsWith("position:", StringComparison.OrdinalIgnoreCase) ? raw["position:".Length..] : raw)
                            .Select(id => positions.TryGetValue(id, out var name) ? name : Label("CandidateOther"))
                            .Distinct().ToList()
                        : []))
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A Platform read: the <c>data</c> envelope or the bare body; null when unreadable.</summary>
    private async Task<JsonElement?> ReadPlatformAsync(string path, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d.Clone() : root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> LiveVersionAsync(JsonElement path, CancellationToken ct)
    {
        if (Str(path, "pathCode") is not { } code) return null;
        return (await ReadItemsAsync($"{PathsBase}?pathCode={Uri.EscapeDataString(code)}&includeArchived=false", ct) ?? [])
            .Where(x => !Same(Str(x, "pathId"), Str(path, "pathId")) && Same(Str(x, "pathStatus"), "published"))
            .Select(x => Str(x, "pathVersion"))
            .FirstOrDefault();
    }

    private object? ArtifactModel(Guid pathId, JsonElement revision, JsonElement? artifact)
    {
        if (artifact is not { } a) return null;
        var revisionId = Str(revision, "revisionId");
        var url = $"/CRM/KnowledgePaths/api/paths/{pathId}/revisions/{revisionId}/artifact?kind=pdf";
        return new
        {
            fileName = Str(a, "fileName"),
            byteSize = a.TryGetProperty("byteSize", out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : 0,
            renderedAt = Str(a, "renderedAt"),
            checksum = Str(a, "checksum"),
            viewUrl = url,
            downloadUrl = url + "&download=true"
        };
    }

    private static JsonElement? Pdf(JsonElement revision)
        => Items(revision, "artifacts").FirstOrDefault(a => Same(Str(a, "kind"), "pdf")) is { ValueKind: JsonValueKind.Object } pdf
            ? pdf
            : null;

    private static bool RoundOpen(JsonElement revision)
        => revision.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.Object && Bool(round, "isOpen");

    private static string? Round(JsonElement revision, string field)
        => revision.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.Object ? Str(round, field) : null;

    private static string? ReleaseState(JsonElement revision) => Release(revision, "state");

    private static string? Release(JsonElement revision, string field)
        => revision.TryGetProperty("release", out var release) && release.ValueKind == JsonValueKind.Object ? Str(release, field) : null;

    private static bool Same(string? a, string? b) => R.Same(a, b);
}
