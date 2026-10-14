using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitReport.V2;

/// <summary>A required answer that is not given (<c>{section, productId?, field}</c>) — the wizard jumps to it.</summary>
public sealed record VisitReportMissingDto(string Section, Guid? ProductId, string Field);

/// <summary>The v2 sections of a report as they travel (draft / submit / amend / read). The classes are the domain's
/// (VisitReportV2.cs) so the stored and the wire shape can never drift.</summary>
public sealed record VisitReportV2Body(
    VisitReportGeneral? General = null,
    IReadOnlyList<VisitReportProductSection>? ProductSections = null,
    VisitReportDoctorLevel? Doctor = null,
    VisitReportPharmacy? Pharmacy = null,
    VisitReportSafety? Safety = null,
    VisitReportFollowUp? FollowUp = null);

/// <summary>What the completeness check needs besides the body (gathered by the handler).</summary>
public sealed record VisitReportV2CheckContext(
    string Kind,
    IReadOnlySet<Guid> PlannedProductIds,
    IReadOnlySet<Guid> DuePotentialProductIds,
    bool TotalDue,
    IReadOnlySet<Guid> CompetitorListedProductIds,
    bool PharmacyCompetitorsListed,
    IReadOnlySet<Guid>? IndicationProductIds = null,
    Guid? PharmacyProductId = null,
    bool PharmacyProductsUnavailable = false);
// WP-VW-W4 (W4-FIX-d, G-9) — PharmacyProductId: the product a pharmacy report's competitor list is probed with and its
// proposals are filed under (the plan's first product, else the first stock / sales GSKU's global product);
// PharmacyProductsUnavailable: MDM could not map the GSKUs (the old plan-only rule applied, the submit warns).
// WP-VW-W4 (W4-FIX-b, F-5) — IndicationProductIds: the products that HAVE approved indication × profile rows (Concept
// Graph). Only those are asked the indication potential. null = not gathered (callers that do not care: every product
// counts as having rows — the pre-FIX-b rule); the report handler always gathers it for a doctor report.

public static class VisitReportV2Sections
{
    public const string General = "general";
    public const string Product = "product";
    public const string Doctor = "doctor";
    public const string Pharmacy = "pharmacy";
    public const string Safety = "safety";
}

/// <summary>
/// WP-VW-W4 (W4-BE-a, §2.5) — the pure rules of the report v2: the kind, the pre-fill from the W3 execution, the plan
/// match, the next-message suggestion, the completeness check on submit (<c>missing[]</c>), the journey target of an
/// adoption stage and the amendment's old / new values. No I/O.
/// </summary>
public static class VisitReportV2Rules
{
    /// <summary>The adoption stages in order (the <c>crm-adoption-stage</c> codes; the rank drives the journey move).</summary>
    public static readonly IReadOnlyList<string> AdoptionStageOrder = new[] { "unaware", "aware", "evaluating", "trying", "regular", "advocate" };

    public static string KindOf(Domain.Entities.PlannedVisit plan)
        => string.Equals(plan.TargetType, PlannedVisitTargetType.Pharmacy, StringComparison.OrdinalIgnoreCase)
            ? VisitReportKinds.Pharmacy
            : VisitReportKinds.Doctor;

    // ---------------- pre-fill (W3 execution → report) ----------------

    /// <summary>
    /// The sections a report starts from: the planned products (in plan order) plus any product the rep presented without
    /// a plan line (<c>Unplanned</c>). Per product: the steps shown + the plan match, the minutes (timer, rounded up,
    /// 1–240), the interest mark, an objection row per objection mark, a request row per sample / literature mark. The
    /// rep's PERSONAL notes never reach the report.
    /// </summary>
    public static VisitReportV2Body Prefill(Domain.Entities.PlannedVisit plan, Domain.Entities.VisitExecution? execution)
    {
        var kind = KindOf(plan);
        var general = new VisitReportGeneral
        {
            VisitKindCode = execution?.Kind ?? VisitExecutionKinds.InPerson,
            CoVisitorUserIds = (execution?.CoVisitorUserIds ?? new List<string>()).ToList(),
            MetContactId = kind == VisitReportKinds.Doctor ? plan.ContactId : null
        };
        if (kind == VisitReportKinds.Pharmacy)
        {
            return new VisitReportV2Body(general, Array.Empty<VisitReportProductSection>(), null,
                new VisitReportPharmacy(), new VisitReportSafety(), new VisitReportFollowUp());
        }

        var planned = (plan.ContentItems ?? new List<PlannedVisitContentItem>()).OrderBy(i => i.Order).Select(i => i.ProductId).Distinct().ToList();
        var shown = (execution?.ProductTimings ?? new List<VisitExecutionProductTiming>()).Select(t => t.ProductId)
            .Concat((execution?.SlideTimings ?? new List<VisitExecutionSlideTiming>()).Select(t => t.ProductId))
            .Where(id => id != Guid.Empty && !planned.Contains(id)).Distinct().ToList();

        var sections = planned.Select(id => Section(plan, execution, id, unplanned: false))
            .Concat(shown.Select(id => Section(plan, execution, id, unplanned: true)))
            .ToList();
        return new VisitReportV2Body(general, sections, new VisitReportDoctorLevel(), null, new VisitReportSafety(), new VisitReportFollowUp());
    }

    private static VisitReportProductSection Section(Domain.Entities.PlannedVisit plan, Domain.Entities.VisitExecution? execution, Guid productId, bool unplanned)
    {
        var section = new VisitReportProductSection { ProductId = productId, Unplanned = unplanned };
        ApplyServerFields(section, plan, execution);
        var seconds = execution?.ProductTimings.Where(t => t.ProductId == productId).Sum(t => t.Seconds) ?? 0;
        section.Minutes = seconds > 0 ? Math.Clamp((seconds + 59) / 60, VisitReportV2Limits.MinMinutes, VisitReportV2Limits.MaxMinutes) : null;

        var mark = execution?.Marks.FirstOrDefault(m => m.ProductId == productId);
        if (mark is not null)
        {
            section.InterestCode = VisitInterestValues.All.Contains(mark.Interest ?? string.Empty) ? mark.Interest : null;
            if (mark.Flags.Contains(VisitMarkFlags.Objection) || !string.IsNullOrWhiteSpace(mark.ObjectionCategory))
            {
                section.Objections.Add(new VisitReportObjection { CategoryCode = mark.ObjectionCategory });
            }

            if (mark.Flags.Contains(VisitMarkFlags.SampleRequest)) section.Requests.Add(new VisitReportRequest { TypeCode = "sample" });
            if (mark.Flags.Contains(VisitMarkFlags.LiteratureRequest)) section.Requests.Add(new VisitReportRequest { TypeCode = "literature" });
        }

        return section;
    }

    // ---------------- the indication potential (W4-FIX-b, F-5) ----------------

    public const string PotentialIgnoredWarning = "potential_ignored_no_indications";

    /// <summary>Does the product have approved indication × profile rows? (null set = not gathered ⇒ yes.)</summary>
    public static bool HasIndicationRows(VisitReportV2CheckContext context, Guid productId)
        => context.IndicationProductIds is null || context.IndicationProductIds.Contains(productId);

    /// <summary>
    /// The ONE rule: the indication potential of a product is asked when it is due (90 days) or the rep updated it, AND the
    /// product has indication rows. Without rows there is nothing to answer — "no patients in these indications" would be a
    /// false statement, so it is neither required nor recorded.
    /// </summary>
    public static bool PotentialAsked(VisitReportV2CheckContext context, VisitReportProductSection section)
        => HasIndicationRows(context, section.ProductId)
           && (context.DuePotentialProductIds.Contains(section.ProductId) || section.Potential?.Updated == true);

    /// <summary>
    /// A potential sent for a product WITHOUT indication rows (rows or "no patients") is dropped from the body before it is
    /// stored or recorded; each drop is a warning <c>potential_ignored_no_indications:{productId}</c>.
    /// </summary>
    public static IReadOnlyList<string> DropPotentialsWithoutIndications(VisitReportV2Body body, VisitReportV2CheckContext context)
    {
        var warnings = new List<string>();
        foreach (var s in body.ProductSections ?? Array.Empty<VisitReportProductSection>())
        {
            if (HasIndicationRows(context, s.ProductId) || s.Potential is not { } p) continue;
            if (p.NoPatientsInIndications == true || p.Rows.Count > 0)
            {
                warnings.Add(PotentialIgnoredWarning + ":" + s.ProductId.ToString("D"));
            }

            s.Potential = null;
        }

        return warnings;
    }

    /// <summary>The members only the server sets (the steps shown, the plan match, the suggestion) — recomputed on every write.</summary>
    public static void ApplyServerFields(VisitReportProductSection section, Domain.Entities.PlannedVisit plan, Domain.Entities.VisitExecution? execution)
    {
        section.PresentedSteps = (execution?.SlideTimings ?? new List<VisitExecutionSlideTiming>())
            .Where(t => t.ProductId == section.ProductId)
            .GroupBy(t => t.StepId)
            .Select(g => new VisitReportPresentedStep { StepId = g.Key, Seconds = g.Sum(t => t.Seconds) })
            .ToList();
        var item = (plan.ContentItems ?? new List<PlannedVisitContentItem>()).FirstOrDefault(i => i.ProductId == section.ProductId);
        section.Unplanned = item is null;
        section.MatchedPlan = item is null || section.PresentedSteps.Count == 0
            ? (item is null ? false : null)
            : section.PresentedSteps.All(s => item.Steps.Any(p => p.StepId == s.StepId));
        section.NextMessageSuggestion = Suggestion(section.CommitmentCode, section.AdoptionStageCode);
        section.PotentialAsked = null; // pre-fill only (W4-FIX-b); never stored
    }

    /// <summary>
    /// The next-message suggestion KEY: a commitment that asks a follow-up wins (<c>commitment:will_try</c>,
    /// <c>commitment:will_prescribe</c>, <c>commitment:wants_info</c>); otherwise the next adoption stage
    /// (<c>stage:{next}</c>); the last stage / nothing chosen ⇒ null. Web words the key in the rep's language.
    /// </summary>
    public static string? Suggestion(string? commitment, string? adoptionStage)
    {
        if (commitment is "will_try" or "will_prescribe" or "wants_info") return "commitment:" + commitment;
        var rank = Rank(adoptionStage);
        return rank >= 0 && rank < AdoptionStageOrder.Count - 1 ? "stage:" + AdoptionStageOrder[rank + 1] : null;
    }

    public static int Rank(string? adoptionStage)
        => string.IsNullOrWhiteSpace(adoptionStage) ? -1 : AdoptionStageOrder.ToList().IndexOf(adoptionStage.Trim().ToLowerInvariant());

    // ---------------- the journey move ----------------

    /// <summary>
    /// The stage index a reported adoption stage moves the journey to: the FIRST stage (in stage order) mapped to an adoption
    /// stage AFTER the reported one. <c>null</c> = use <see cref="JourneyProgress.Advance"/> (no stage of the journey is
    /// mapped, or no adoption stage was reported); <c>-1</c> = mapped, but nothing after the reported stage (stay).
    /// </summary>
    public static int? JourneyTarget(IReadOnlyList<ContentEngagementJourneyStage> orderedStages, string? adoptionStage)
    {
        if (!orderedStages.Any(s => Rank(s.AdoptionStageCode) >= 0)) return null;
        var reported = Rank(adoptionStage);
        if (reported < 0) return null;
        for (var i = 0; i < orderedStages.Count; i++)
        {
            if (Rank(orderedStages[i].AdoptionStageCode) > reported) return i;
        }

        return -1;
    }

    // ---------------- completeness (submit) ----------------

    public static IReadOnlyList<VisitReportMissingDto> Missing(VisitReportV2Body body, VisitReportV2CheckContext context)
    {
        var missing = new List<VisitReportMissingDto>();
        void Need(bool ok, string section, Guid? product, string field)
        {
            if (!ok) missing.Add(new VisitReportMissingDto(section, product, field));
        }

        var general = body.General;
        Need(VisitExecutionKinds.IsKnown(general?.VisitKindCode), VisitReportV2Sections.General, null, "visitKindCode");
        var safety = body.Safety;
        Need(safety?.AdverseEvent is not null, VisitReportV2Sections.Safety, null, "adverseEvent");
        Need(safety?.AdverseEvent != true || !string.IsNullOrWhiteSpace(safety.AdverseEventNote), VisitReportV2Sections.Safety, null, "adverseEventNote");

        if (context.Kind == VisitReportKinds.Pharmacy)
        {
            var pharmacy = body.Pharmacy;
            Need(!string.IsNullOrWhiteSpace(general?.MetRoleCode), VisitReportV2Sections.General, null, "metRoleCode");
            foreach (var row in pharmacy?.Stock ?? new List<VisitReportStockRow>())
            {
                Need(!string.IsNullOrWhiteSpace(row.LevelCode), VisitReportV2Sections.Pharmacy, row.GskuId, "stock.levelCode");
            }

            foreach (var row in pharmacy?.Sales ?? new List<VisitReportSalesRow>())
            {
                Need(!string.IsNullOrWhiteSpace(row.TrendCode), VisitReportV2Sections.Pharmacy, row.GskuId, "sales.trendCode");
            }

            Need(!context.PharmacyCompetitorsListed || (pharmacy?.Competitors.Count ?? 0) > 0 || pharmacy?.NoCompetitor == true,
                VisitReportV2Sections.Pharmacy, null, "competitors");
            return missing;
        }

        Need(general?.MetContactId is not null, VisitReportV2Sections.General, null, "metContactId");
        Need(safety?.OffLabelRequest is not null, VisitReportV2Sections.Safety, null, "offLabelRequest");
        Need(!context.TotalDue || body.Doctor?.TotalMonthlyPatients is not null, VisitReportV2Sections.Doctor, null, "totalMonthlyPatients");

        var sections = body.ProductSections ?? Array.Empty<VisitReportProductSection>();
        foreach (var planned in context.PlannedProductIds.Where(p => sections.All(s => s.ProductId != p)))
        {
            Need(false, VisitReportV2Sections.Product, planned, "section");
        }

        foreach (var s in sections)
        {
            var id = s.ProductId;
            Need(s.Minutes is >= VisitReportV2Limits.MinMinutes and <= VisitReportV2Limits.MaxMinutes, VisitReportV2Sections.Product, id, "minutes");
            Need(!string.IsNullOrWhiteSpace(s.VisitSuccessCode), VisitReportV2Sections.Product, id, "visitSuccessCode");
            Need(!string.IsNullOrWhiteSpace(s.CommitmentCode), VisitReportV2Sections.Product, id, "commitmentCode");
            Need(!string.IsNullOrWhiteSpace(s.InterestCode), VisitReportV2Sections.Product, id, "interestCode");
            Need(!string.IsNullOrWhiteSpace(s.AdoptionStageCode), VisitReportV2Sections.Product, id, "adoptionStageCode");
            Need(!string.IsNullOrWhiteSpace(s.ShareBucketCode), VisitReportV2Sections.Product, id, "shareBucketCode");
            Need(!PotentialAsked(context, s) || s.Potential?.NoPatientsInIndications == true || (s.Potential?.Rows.Count ?? 0) > 0,
                VisitReportV2Sections.Product, id, "potential");
            Need(!context.CompetitorListedProductIds.Contains(id) || s.Competitors.Count > 0 || s.NoCompetitor,
                VisitReportV2Sections.Product, id, "competitors");
            Need(s.Objections.Count > 0 || s.NoObjection, VisitReportV2Sections.Product, id, "objections");
            Need(s.Requests.Count > 0 || s.NoRequest, VisitReportV2Sections.Product, id, "requests");
            Need(s.Sample is not null || s.NoSample, VisitReportV2Sections.Product, id, "sample");
            Need(!string.IsNullOrWhiteSpace(s.NextMessage), VisitReportV2Sections.Product, id, "nextMessage");
            foreach (var objection in s.Objections)
            {
                Need(!string.IsNullOrWhiteSpace(objection.CategoryCode), VisitReportV2Sections.Product, id, "objections.categoryCode");
                Need(VisitReportObjectionResolutions.All.Contains(objection.Resolved ?? string.Empty), VisitReportV2Sections.Product, id, "objections.resolved");
            }

            foreach (var request in s.Requests)
            {
                Need(!string.IsNullOrWhiteSpace(request.TypeCode), VisitReportV2Sections.Product, id, "requests.typeCode");
            }
        }

        return missing;
    }

    /// <summary>The shape limits (any time, also on a draft): lengths, ranges, list sizes. Null = valid.</summary>
    public static string? Shape(VisitReportV2Body body)
    {
        if ((body.General?.GeneralNote?.Length ?? 0) > VisitReportV2Limits.MaxGeneralNote) return "generalNote is at most 4000 characters.";
        if (body.General?.VisitKindCode is { } kind && !VisitExecutionKinds.IsKnown(kind)) return "visitKindCode is in_person or remote.";
        var sections = body.ProductSections ?? Array.Empty<VisitReportProductSection>();
        if (sections.Count > VisitReportV2Limits.MaxRows || sections.Any(s => s.ProductId == Guid.Empty)
            || sections.Select(s => s.ProductId).Distinct().Count() != sections.Count)
        {
            return "Each product once, a Global Product id each (at most 50).";
        }

        foreach (var s in sections)
        {
            if (s.Minutes is { } m && (m < VisitReportV2Limits.MinMinutes || m > VisitReportV2Limits.MaxMinutes)) return "minutes is 1–240.";
            if ((s.NextMessage?.Length ?? 0) > VisitReportV2Limits.MaxNextMessage) return "nextMessage is at most 500 characters.";
            if (s.Sample is { } sample && (sample.GskuId == Guid.Empty || sample.Quantity < VisitReportV2Limits.MinSampleQuantity
                                           || sample.Quantity > VisitReportV2Limits.MaxSampleQuantity))
            {
                return "A sample is a GSKU and 1–100 units.";
            }

            if (s.Competitors.Any(c => c.CompetitorProductId is null && (!c.Proposed || string.IsNullOrWhiteSpace(c.NameSnapshot) || string.IsNullOrWhiteSpace(c.Company)))
                || s.Competitors.Any(c => c.NameSnapshot.Length > VisitReportV2Limits.MaxNameLength))
            {
                return "A competitor is an MDM record, or a proposal with a name and a company.";
            }

            if (s.Objections.Any(o => o.Resolved is { } r && !VisitReportObjectionResolutions.All.Contains(r))) return "resolved is yes, partly or no.";
            if (s.Potential is { } p && (p.Rows.Count > VisitReportV2Limits.MaxRows || p.Rows.Any(r => r.MonthlyPatients < 0 || r.IndicationNodeId == Guid.Empty)
                                         || (p.NoPatientsInIndications == true && p.Rows.Count > 0)))
            {
                return "The potential is rows of 0+ patients, or 'no patients' without rows.";
            }

            if (s.Objections.Concat<object>(s.Requests).Count() > VisitReportV2Limits.MaxRows) return "Too many rows.";
        }

        return null;
    }

    /// <summary>Every (set, code, path) of the body, for the BRD check on submit.</summary>
    public static IEnumerable<(string Set, string Code, string Path)> Codes(VisitReportV2Body body)
    {
        static IEnumerable<(string, string, string)> One(string set, string? code, string path)
            => string.IsNullOrWhiteSpace(code) ? Array.Empty<(string, string, string)>() : new[] { (set, code.Trim(), path) };

        foreach (var s in body.ProductSections ?? Array.Empty<VisitReportProductSection>())
        {
            var at = $"productSections[{s.ProductId:D}]";
            foreach (var x in One(VisitReportV2ReferenceSets.VisitSuccess, s.VisitSuccessCode, at + ".visitSuccessCode")) yield return x;
            foreach (var x in One(VisitReportV2ReferenceSets.DoctorCommitment, s.CommitmentCode, at + ".commitmentCode")) yield return x;
            foreach (var x in One(VisitReportV2ReferenceSets.VisitInterest, s.InterestCode, at + ".interestCode")) yield return x;
            foreach (var x in One(DoctorInsightsReferenceSets.AdoptionStage, s.AdoptionStageCode, at + ".adoptionStageCode")) yield return x;
            foreach (var x in One(DoctorInsightsReferenceSets.ShareBucket, s.ShareBucketCode, at + ".shareBucketCode")) yield return x;
            foreach (var r in s.Requests) foreach (var x in One(VisitReportV2ReferenceSets.VisitRequestType, r.TypeCode, at + ".requests.typeCode")) yield return x;
            foreach (var o in s.Objections) foreach (var x in One("visit-objection-category", o.CategoryCode, at + ".objections.categoryCode")) yield return x;
            foreach (var c in s.Competitors)
            foreach (var code in c.AdvantageCodes.Concat(c.DisadvantageCodes))
            foreach (var x in One(VisitReportV2ReferenceSets.CompetitorAdvantage, code, at + ".competitors.advantageCodes")) yield return x;
        }

        var p = body.Pharmacy;
        foreach (var x in One(VisitReportV2ReferenceSets.PharmacyMetRole, body.General?.MetRoleCode, "general.metRoleCode")) yield return x;
        if (p is null) yield break;
        foreach (var row in p.Stock) foreach (var x in One(VisitReportV2ReferenceSets.PharmacyStockLevel, row.LevelCode, "pharmacy.stock.levelCode")) yield return x;
        foreach (var x in One(VisitReportV2ReferenceSets.ShelfPosition, p.Shelf?.PositionCode, "pharmacy.shelf.positionCode")) yield return x;
        foreach (var row in p.Sales) foreach (var x in One(VisitReportV2ReferenceSets.SalesTrend, row.TrendCode, "pharmacy.sales.trendCode")) yield return x;
        foreach (var x in One(VisitReportV2ReferenceSets.RecommendationLevel, p.Recommendation?.LevelCode, "pharmacy.recommendation.levelCode")) yield return x;
        foreach (var r in p.Training?.Requests ?? new List<VisitReportRequest>())
        foreach (var x in One(VisitReportV2ReferenceSets.VisitRequestType, r.TypeCode, "pharmacy.training.requests.typeCode")) yield return x;
        foreach (var c in p.Competitors)
        foreach (var code in c.AdvantageCodes.Concat(c.DisadvantageCodes))
        foreach (var x in One(VisitReportV2ReferenceSets.CompetitorAdvantage, code, "pharmacy.competitors.advantageCodes")) yield return x;
    }

    // ---------------- apply / read ----------------

    public static void Apply(Domain.Entities.VisitReport report, VisitReportV2Body body, string kind)
    {
        report.SchemaVersion = VisitReportSchemaVersions.V2;
        report.ReportKind = kind;
        report.General = body.General ?? new VisitReportGeneral();
        report.ProductSections = kind == VisitReportKinds.Doctor ? (body.ProductSections ?? Array.Empty<VisitReportProductSection>()).ToList() : new List<VisitReportProductSection>();
        report.Doctor = kind == VisitReportKinds.Doctor ? body.Doctor ?? new VisitReportDoctorLevel() : null;
        report.Pharmacy = kind == VisitReportKinds.Pharmacy ? body.Pharmacy ?? new VisitReportPharmacy() : null;
        report.Safety = body.Safety ?? new VisitReportSafety();
        if (kind == VisitReportKinds.Pharmacy) report.Safety.OffLabelRequest = null; // no off-label question at a pharmacy
        report.FollowUp = body.FollowUp ?? new VisitReportFollowUp();
    }

    public static VisitReportV2Body BodyOf(Domain.Entities.VisitReport report)
        => new(report.General, report.ProductSections, report.Doctor, report.Pharmacy, report.Safety, report.FollowUp);

    // ---------------- amendment: old / new values ----------------

    // Relaxed escaping: the stored old / new values stay readable (Turkish, Arabic …); they are data, never put into HTML unencoded.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// The changed values between two bodies, as <c>{path, old, new}</c> with JSON values: product sections are keyed by
    /// product (<c>productSections[{id}].shareBucketCode</c>), objects are walked member by member, a list is one value.
    /// </summary>
    public static IReadOnlyList<VisitReportAmendmentChange> Diff(object? before, object? after, string root)
    {
        var changes = new List<VisitReportAmendmentChange>();
        Walk(ToNode(before), ToNode(after), root, changes);
        return changes;
    }

    public static IReadOnlyList<VisitReportAmendmentChange> DiffBodies(VisitReportV2Body before, VisitReportV2Body after)
    {
        var changes = new List<VisitReportAmendmentChange>();
        changes.AddRange(Diff(before.General, after.General, "general"));
        var oldSections = (before.ProductSections ?? Array.Empty<VisitReportProductSection>()).ToDictionary(s => s.ProductId);
        var newSections = (after.ProductSections ?? Array.Empty<VisitReportProductSection>()).ToDictionary(s => s.ProductId);
        foreach (var id in oldSections.Keys.Union(newSections.Keys).OrderBy(x => x))
        {
            changes.AddRange(Diff(oldSections.GetValueOrDefault(id), newSections.GetValueOrDefault(id), $"productSections[{id:D}]"));
        }

        changes.AddRange(Diff(before.Doctor, after.Doctor, "doctor"));
        changes.AddRange(Diff(before.Pharmacy, after.Pharmacy, "pharmacy"));
        changes.AddRange(Diff(before.Safety, after.Safety, "safety"));
        changes.AddRange(Diff(before.FollowUp, after.FollowUp, "followUp"));
        return changes;
    }

    private static JsonNode? ToNode(object? value) => value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType(), Json);

    private static void Walk(JsonNode? before, JsonNode? after, string path, List<VisitReportAmendmentChange> changes)
    {
        if (before is JsonObject b && after is JsonObject a)
        {
            foreach (var key in b.Select(p => p.Key).Union(a.Select(p => p.Key)).Distinct(StringComparer.Ordinal))
            {
                Walk(b[key], a[key], path + "." + key, changes);
            }

            return;
        }

        var old = before?.ToJsonString(Json);
        var @new = after?.ToJsonString(Json);
        if (!string.Equals(Normalize(old), Normalize(@new), StringComparison.Ordinal))
        {
            changes.Add(new VisitReportAmendmentChange { Path = path, Old = old, New = @new });
        }
    }

    private static string? Normalize(string? json) => json is null or "null" or "[]" or "\"\"" or "false" ? null : json;
}
