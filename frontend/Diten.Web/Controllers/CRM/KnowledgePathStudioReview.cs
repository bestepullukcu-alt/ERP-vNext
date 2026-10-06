using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-UI-2 — the pure rules behind the studio's compliance, MLR, revision and release screens. Everything here turns
/// CRM / MOD-0023 data into LABELLED rows (the resx text through <c>label</c>); no raw code leaves this class. CRM stays
/// the authority: these rows only explain, ahead of time, what CRM would refuse.
/// </summary>
public static class KnowledgePathStudioReview
{
    // ---------------- who is asking (person-based SoD, D-KP-8) ----------------

    /// <summary>The session user exactly as CRM names the actor (HttpActorContext): <c>sub</c>, then NameIdentifier,
    /// then e-mail, then the identity name. Web validates the SAME JWT, so both sides read the same value.</summary>
    public static string? ActorOf(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        var value = user.FindFirst("sub")?.Value
                    ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst("email")?.Value
                    ?? user.Identity.Name;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Same person = the CRM rule (KnowledgePathReviewRules.SamePerson): trimmed, case-insensitive, never
    /// for an empty side.</summary>
    public static bool SamePerson(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
           && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The caller submitted this revision (its creator or the round's submitter) — the decision panel is not
    /// offered and the release precondition fails. CRM refuses the same person with 403 anyway.</summary>
    public static bool IsSubmitter(string? actor, JsonElement revision)
        => SamePerson(actor, Str(revision, "submittedBy"))
           || (revision.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.Object
               && SamePerson(actor, Str(round, "submittedBy")));

    // ---------------- compliance (kurgu rules until KP-UI-3) ----------------

    public const string Blocker = "blocker";
    public const string Warning = "warning";

    public sealed record ComplianceRow(
        string Rule, string Severity, string RuleLabel, string SeverityLabel, string Where, string Message, string Fix,
        string? BranchCode, string? ChainStepId, string? ItemKind, string? ItemId);

    public static class Rules
    {
        public const string Chain = "chain";
        public const string Published = "published";
        public const string Language = "language";
        public const string Claim = "claim";
        public const string Placement = "placement";
    }

    public sealed record SlotName(string BranchCode, string BranchName, string ChainStepId, string StepName);

    /// <summary>The compliance rows of a chain-bound path: chain conformance (under / over), steps not placed on the
    /// chain, contents not published or in another language, claims not usable in the path country / language. Rows
    /// follow the chain order (branch, step), then item order.</summary>
    public static IReadOnlyList<ComplianceRow> Compliance(
        JsonElement path, IReadOnlyDictionary<string, JsonElement> contents, IReadOnlyList<SlotName> slots,
        Func<string, string> label, Func<string?, string?> languageName, Func<string?, string?> statusLabel)
    {
        var rows = new List<ComplianceRow>();
        var language = Str(path, "languageCode");
        var pathLanguage = languageName(language) ?? string.Empty;
        var country = Str(path, "countryCode");
        var countryName = string.IsNullOrWhiteSpace(country) ? string.Empty
            : ClaimDisplayNames.CountryName(country.ToUpperInvariant()) ?? country;
        var steps = Items(path, "steps").Where(s => s.ValueKind == JsonValueKind.Object && !Bool(s, "isArchived")).ToList();
        var claims = Items(path, "claims").Where(c => c.ValueKind == JsonValueKind.Object).ToList();
        var conformance = Items(path, "chainConformance").Where(c => c.ValueKind == JsonValueKind.Object).ToList();

        string WhereOf(SlotName? slot, string? item = null)
            => string.Join(" › ", new[] { slot?.BranchName, slot?.StepName, item }.Where(x => !string.IsNullOrWhiteSpace(x)));
        SlotName? SlotOf(JsonElement item)
        {
            if (!item.TryGetProperty("arrangement", out var a) || a.ValueKind != JsonValueKind.Object) return null;
            var code = Str(a, "branchCode");
            var stepId = Str(a, "chainStepId");
            return slots.FirstOrDefault(s => Same(s.BranchCode, code) && Same(s.ChainStepId, stepId));
        }
        ComplianceRow Row(string rule, string severity, SlotName? slot, string? itemTitle, string message, string fix,
            string? kind = null, string? id = null)
            => new(rule, severity, label("Rule_" + rule), label("Sev_" + severity), WhereOf(slot, itemTitle), message, fix,
                slot?.BranchCode, slot?.ChainStepId, kind, id);

        foreach (var slot in slots)
        {
            var conf = conformance.FirstOrDefault(c => Same(Str(c, "branchCode"), slot.BranchCode) && Same(Str(c, "chainStepId"), slot.ChainStepId));
            if (conf.ValueKind != JsonValueKind.Object) continue;
            var count = Int(conf, "count") ?? 0;
            switch (Str(conf, "status"))
            {
                case "under":
                    rows.Add(Row(Rules.Chain, Blocker, slot, null,
                        Format(label("Comp_UnderMsg"), slot.StepName, Int(conf, "min") ?? 0, count), label("Comp_UnderFix")));
                    break;
                case "over":
                    rows.Add(Row(Rules.Chain, Blocker, slot, null,
                        Format(label("Comp_OverMsg"), slot.StepName, Int(conf, "max") ?? 0, count), label("Comp_OverFix")));
                    break;
            }
        }

        foreach (var step in steps.OrderBy(s => Int(s, "stepOrder") ?? int.MaxValue))
        {
            var content = Str(step, "contentId") is { } id && contents.TryGetValue(id, out var c) ? c : (JsonElement?)null;
            var title = (content is { } ct ? Str(ct, "contentTitle") : null) ?? Str(step, "stepTitle") ?? string.Empty;
            var slot = SlotOf(step);
            var stepId = Str(step, "stepId");
            if (slot is null)
            {
                rows.Add(Row(Rules.Placement, Blocker, null, title, Format(label("Comp_UnplacedMsg"), title), label("Comp_UnplacedFix"),
                    "content", stepId));
                continue;
            }

            if (content is not { } body) continue;
            var status = Str(body, "contentStatus");
            if (!Same(status, "published"))
            {
                rows.Add(Row(Rules.Published, Blocker, slot, title,
                    Format(label("Comp_NotPublishedMsg"), title, statusLabel(status) ?? string.Empty), label("Comp_NotPublishedFix"),
                    "content", stepId));
            }

            var contentLanguage = Str(body, "languageCode");
            if (!string.IsNullOrWhiteSpace(language) && !Same(contentLanguage, language))
            {
                rows.Add(Row(Rules.Language, Blocker, slot, title,
                    Format(label("Comp_LanguageMsg"), title, languageName(contentLanguage) ?? string.Empty, pathLanguage),
                    Format(label("Comp_LanguageFix"), pathLanguage), "content", stepId));
            }
        }

        foreach (var claim in claims.OrderBy(c => slots.ToList().FindIndex(s => s == SlotOf(c))))
        {
            if (claim.TryGetProperty("usable", out var usable) && usable.ValueKind == JsonValueKind.True) continue;
            var code = Str(claim, "claimCode") ?? string.Empty;
            var slot = SlotOf(claim);
            var (message, fix) = Str(claim, "reason") switch
            {
                "no_country_version" => (Format(label("Comp_ClaimNoVersionMsg"), code, countryName), Format(label("Comp_ClaimNoVersionFix"), countryName)),
                "language_mismatch" => (Format(label("Comp_ClaimLanguageMsg"), code, pathLanguage), Format(label("Comp_ClaimLanguageFix"), pathLanguage)),
                _ => (Format(label("Comp_ClaimNotApprovedMsg"), code, countryName), label("Comp_ClaimNotApprovedFix"))
            };
            rows.Add(Row(Rules.Claim, Blocker, slot, code, message, fix, "claim", Str(claim, "claimId")));
        }

        return rows;
    }

    // ---------------- MLR timeline (review-history + the template plan) ----------------

    public sealed record PlanStep(string Code, string? Name, IReadOnlyList<string> Candidates);

    public sealed record HistoryEntry(
        string Action, string? StepCode, string? StepName, string? Comment, string? ActorId, string? ActorDisplay, DateTimeOffset? At);

    public sealed record TimelineStep(
        string? StepCode, string StepName, string State, string StateLabel, string? Actor, DateTimeOffset? At, string? Comment,
        IReadOnlyList<string> Candidates);

    public static class StepStates
    {
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string Pending = "pending";
        public const string Queued = "queued";
        public const string Cancelled = "cancelled";
        public const string NotReached = "not-reached";
    }

    private static readonly string[] DecisionActions = ["approve", "reject", "cancel", "timeout"];

    public static IReadOnlyList<HistoryEntry> ReadHistory(JsonElement? revisionHistory)
        => revisionHistory is { ValueKind: JsonValueKind.Object } h
            ? Items(h, "entries").Where(e => e.ValueKind == JsonValueKind.Object).Select(e => new HistoryEntry(
                (Str(e, "action") ?? string.Empty).ToLowerInvariant(), Str(e, "stepCode"), Str(e, "stepName"), Str(e, "comment"),
                Str(e, "actorId"), Str(e, "actorDisplay"),
                e.TryGetProperty("occurredAt", out var at) && at.ValueKind == JsonValueKind.String && at.TryGetDateTimeOffset(out var d) ? d : null))
              .ToList()
            : [];

    /// <summary>One row per MLR step in plan order (Medical → Legal → Regulatory): the decision (state, person, time,
    /// comment) from MOD-0023 history; while the round is open, the first undecided step is "pending" (with its
    /// candidate positions) and the rest "queued"; a closed round leaves the rest "not reached". Without the plan the
    /// steps come from the history in order. A step is never shown by its code.</summary>
    public static IReadOnlyList<TimelineStep> Timeline(
        IReadOnlyList<PlanStep>? plan, IReadOnlyList<HistoryEntry> history, bool roundOpen,
        Func<string, string> label, Func<string?, string?, string> person)
    {
        var order = plan is { Count: > 0 }
            ? plan.Select(p => (p.Code, p.Name, p.Candidates)).ToList()
            : history.Where(h => !string.IsNullOrWhiteSpace(h.StepCode)).Select(h => h.StepCode!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(code => (Code: code, Name: (string?)null, Candidates: (IReadOnlyList<string>)Array.Empty<string>())).ToList();

        var rows = new List<TimelineStep>();
        var pendingGiven = false;
        for (var i = 0; i < order.Count; i++)
        {
            var (code, planName, candidates) = order[i];
            var mine = history.Where(h => Same(h.StepCode, code)).ToList();
            var name = planName ?? mine.Select(h => h.StepName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                       ?? Format(label("MlrStepN"), i + 1);
            var decision = mine.LastOrDefault(h => DecisionActions.Contains(h.Action));
            string state;
            if (decision is not null)
            {
                state = decision.Action switch
                {
                    "approve" => StepStates.Approved,
                    "reject" => StepStates.Rejected,
                    _ => StepStates.Cancelled
                };
            }
            else if (roundOpen && !pendingGiven)
            {
                state = StepStates.Pending;
                pendingGiven = true;
            }
            else
            {
                state = roundOpen ? StepStates.Queued : StepStates.NotReached;
            }

            var opened = mine.LastOrDefault(h => h.Action == "start");
            rows.Add(new TimelineStep(code, name, state, label("MlrState_" + state),
                decision is null ? null : person(decision.ActorId, decision.ActorDisplay),
                decision?.At ?? (state == StepStates.Pending ? opened?.At : null),
                decision?.Comment,
                state == StepStates.Pending ? candidates : []));
        }

        return rows;
    }

    // ---------------- revision difference (two frozen snapshots, or a snapshot and the draft) ----------------

    public sealed record DiffItem(string Key, string Label, string? Slot, int Position, string? Version, string? Content);

    public sealed record DiffRow(string Kind, string KindLabel, string Label, string? From, string? To);

    /// <summary>The changes from <paramref name="from"/> to <paramref name="to"/> — the CRM ChangeSummary vocabulary
    /// (step added / removed / moved, content changed, content version changed, claim added / removed / moved,
    /// claim version changed), keyed by step code and claim code.</summary>
    public static IReadOnlyList<DiffRow> Diff(
        IReadOnlyList<DiffItem> fromSteps, IReadOnlyList<DiffItem> toSteps,
        IReadOnlyList<DiffItem> fromClaims, IReadOnlyList<DiffItem> toClaims, Func<string, string> label)
    {
        var rows = new List<DiffRow>();
        DiffRow Row(string kind, string text, string? from, string? to) => new(kind, label("Change_" + kind), text, from, to);

        foreach (var step in toSteps)
        {
            var before = fromSteps.FirstOrDefault(s => Same(s.Key, step.Key));
            if (before is null) { rows.Add(Row("step-added", step.Label, null, Join(step.Content, step.Version))); continue; }
            if (!Same(before.Content, step.Content)) rows.Add(Row("content-changed", step.Label, before.Content, step.Content));
            else if (!Same(before.Version, step.Version)) rows.Add(Row("content-version-changed", step.Label, before.Version, step.Version));
            if (!Same(before.Slot, step.Slot) || before.Position != step.Position)
                rows.Add(Row("step-moved", step.Label, before.Slot, step.Slot));
        }

        rows.AddRange(fromSteps.Where(s => !toSteps.Any(t => Same(t.Key, s.Key)))
            .Select(s => Row("step-removed", s.Label, Join(s.Content, s.Version), null)));

        foreach (var claim in toClaims)
        {
            var before = fromClaims.FirstOrDefault(c => Same(c.Key, claim.Key));
            if (before is null) { rows.Add(Row("claim-added", claim.Label, null, claim.Version)); continue; }
            if (!Same(before.Version, claim.Version)) rows.Add(Row("claim-version-changed", claim.Label, before.Version, claim.Version));
            if (!Same(before.Slot, claim.Slot) || before.Position != claim.Position)
                rows.Add(Row("claim-moved", claim.Label, before.Slot, claim.Slot));
        }

        rows.AddRange(fromClaims.Where(c => !toClaims.Any(t => Same(t.Key, c.Key)))
            .Select(c => Row("claim-removed", c.Label, c.Version, null)));
        return rows;
    }

    private static string? Join(string? a, string? b)
        => string.IsNullOrWhiteSpace(a) ? b : string.IsNullOrWhiteSpace(b) ? a : $"{a} · {b}";

    // ---------------- release preconditions ----------------

    public sealed record Precondition(string Key, string Label, bool Ok, string? Reason);

    /// <summary>The KP-3 release gate, explained ahead of time (CRM decides): an approved revision, the archive PDF, the
    /// compliance categories CRM re-checks (contents published, one language, claims usable, chain conformance) and
    /// the person rule "the publisher is not the submitter".</summary>
    public static IReadOnlyList<Precondition> ReleasePreconditions(
        bool approved, bool hasArtifact, bool isSubmitter, IReadOnlyList<ComplianceRow> compliance, int revisionNumber,
        Func<string, string> label)
    {
        bool Clean(params string[] rules) => !compliance.Any(r => r.Severity == Blocker && rules.Contains(r.Rule));
        Precondition Item(string key, bool ok, string? failText)
            => new(key, label("Pre_" + key), ok, ok ? null : failText);

        return
        [
            Item("approved", approved, Format(label("Pre_approvedNo"), revisionNumber)),
            Item("artifact", hasArtifact, label("Pre_artifactNo")),
            Item("published", Clean(Rules.Published), label("Pre_publishedNo")),
            Item("language", Clean(Rules.Language), label("Pre_languageNo")),
            Item("claims", Clean(Rules.Claim), label("Pre_claimsNo")),
            Item("conformance", Clean(Rules.Chain, Rules.Placement), label("Pre_conformanceNo")),
            Item("sod", !isSubmitter, label("Pre_sodNo"))
        ];
    }

    // ---------------- helpers ----------------

    public static string Format(string template, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, template, args.Select(a => a ?? string.Empty).ToArray());

    public static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string? Str(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(p.GetString())
            ? p.GetString()
            : null;

    public static int? Int(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
           && p.TryGetInt32(out var n) ? n : null;

    public static bool Bool(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    public static IEnumerable<JsonElement> Items(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray()
            : [];
}
