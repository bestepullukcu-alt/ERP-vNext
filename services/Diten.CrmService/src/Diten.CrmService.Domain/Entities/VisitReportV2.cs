namespace Diten.CrmService.Domain.Entities;

// WP-VW-W4 (W4-BE-a, §2.5) — the visit report v2 sections. VisitReport GROWS (no new aggregate): a v1 report keeps
// SchemaVersion 1 and every section below null / empty; a v2 report carries the doctor (product sections + doctor level)
// or the pharmacy shape. The same classes are the wire shape of the v2 draft / submit / amend bodies (server-computed
// members — PresentedSteps, MatchedPlan, NextMessageSuggestion — are always recomputed, never taken from a client).

/// <summary>The report's general part (both kinds).</summary>
public sealed class VisitReportGeneral
{
    /// <summary><c>in_person</c> | <c>remote</c> (<see cref="VisitExecutionKinds"/>; pre-filled from the execution).</summary>
    public string? VisitKindCode { get; set; }

    /// <summary>The co-visitors (W3 execution).</summary>
    public List<string> CoVisitorUserIds { get; set; } = new();

    /// <summary>Doctor report: the contact met (pre-filled with the planned doctor).</summary>
    public Guid? MetContactId { get; set; }

    /// <summary>Pharmacy report: <c>crm-pharmacy-met-role</c>.</summary>
    public string? MetRoleCode { get; set; }

    public string? GeneralNote { get; set; }
}

/// <summary>One product of a doctor report (§2.5).</summary>
public sealed class VisitReportProductSection
{
    public Guid ProductId { get; set; }

    /// <summary>Added by the rep (not in the plan): never advances a journey.</summary>
    public bool Unplanned { get; set; }

    /// <summary>The path steps ACTUALLY shown (W3 slide timings) — server-set, read-only summary.</summary>
    public List<VisitReportPresentedStep> PresentedSteps { get; set; } = new();

    /// <summary>Server: every presented step belongs to the planned path of this product.</summary>
    public bool? MatchedPlan { get; set; }

    /// <summary>1–240 (pre-filled from the product timing, correctable).</summary>
    public int? Minutes { get; set; }

    /// <summary><c>crm-visit-success</c>.</summary>
    public string? VisitSuccessCode { get; set; }

    /// <summary><c>crm-doctor-commitment</c>.</summary>
    public string? CommitmentCode { get; set; }

    /// <summary><c>crm-visit-interest</c> (pre-filled from the W3 mark).</summary>
    public string? InterestCode { get; set; }

    /// <summary><c>crm-adoption-stage</c> — moves the content journey on submit.</summary>
    public string? AdoptionStageCode { get; set; }

    public VisitReportPotential? Potential { get; set; }

    /// <summary><c>crm-share-bucket</c>.</summary>
    public string? ShareBucketCode { get; set; }

    public List<VisitReportCompetitor> Competitors { get; set; } = new();
    public bool NoCompetitor { get; set; }

    public List<VisitReportObjection> Objections { get; set; } = new();
    public bool NoObjection { get; set; }

    public List<VisitReportRequest> Requests { get; set; } = new();
    public bool NoRequest { get; set; }

    public VisitReportV2Sample? Sample { get; set; }
    public bool NoSample { get; set; }

    /// <summary>The rep's message for the next visit (≤ 500).</summary>
    public string? NextMessage { get; set; }

    /// <summary>Server: a suggestion KEY (<c>commitment:{code}</c> | <c>stage:{next adoption stage}</c>); Web words it.</summary>
    public string? NextMessageSuggestion { get; set; }
}

public sealed class VisitReportPresentedStep
{
    public Guid StepId { get; set; }
    public int Seconds { get; set; }
}

/// <summary>The patient numbers of one product (asked when due or when the rep updates them).</summary>
public sealed class VisitReportPotential
{
    public List<VisitReportPotentialRow> Rows { get; set; } = new();
    public bool? NoPatientsInIndications { get; set; }

    /// <summary>The rep pressed "update" although the numbers were not due.</summary>
    public bool Updated { get; set; }
}

public sealed class VisitReportPotentialRow
{
    public Guid IndicationNodeId { get; set; }
    public Guid? ProfileNodeId { get; set; }
    public int MonthlyPatients { get; set; }
}

/// <summary>A competitor the doctor / pharmacy uses (MDM competitor product, or the rep's proposal).</summary>
public sealed class VisitReportCompetitor
{
    /// <summary>The MDM record; null for a proposal not yet sent (set when MDM accepted it on submit).</summary>
    public Guid? CompetitorProductId { get; set; }

    public string NameSnapshot { get; set; } = string.Empty;

    /// <summary>The proposal's company (a proposal needs name + company).</summary>
    public string? Company { get; set; }

    public bool Proposed { get; set; }

    /// <summary>Submit forwarded the proposal and MDM answered with its id; false = MDM was unreachable (kept, retried on amend).</summary>
    public bool ProposalForwarded { get; set; }

    /// <summary><c>crm-competitor-advantage</c> (optional).</summary>
    public List<string> AdvantageCodes { get; set; } = new();
    public List<string> DisadvantageCodes { get; set; } = new();
}

public sealed class VisitReportObjection
{
    /// <summary><c>visit-objection-category</c> (W3).</summary>
    public string? CategoryCode { get; set; }

    /// <summary>The W3 library answer used.</summary>
    public Guid? ObjectionResponseId { get; set; }

    public string? Note { get; set; }

    /// <summary><c>yes</c> | <c>partly</c> | <c>no</c>.</summary>
    public string? Resolved { get; set; }
}

public sealed class VisitReportRequest
{
    /// <summary><c>crm-visit-request-type</c>.</summary>
    public string? TypeCode { get; set; }

    public DateOnly? DueDate { get; set; }
    public string? Note { get; set; }
}

/// <summary>A sample handed over (GSKU). The signature is the W3 execution's.</summary>
public sealed class VisitReportV2Sample
{
    public Guid GskuId { get; set; }
    public int Quantity { get; set; }
    public string? Lot { get; set; }
}

/// <summary>The doctor level of a doctor report (to the doctor insights recorder on submit).</summary>
public sealed class VisitReportDoctorLevel
{
    public string? AdoptionTypologyCode { get; set; }
    public string? CommunicationStyleCode { get; set; }
    public string? KolLevelCode { get; set; }

    /// <summary>Required when the profile's total is due (three months).</summary>
    public int? TotalMonthlyPatients { get; set; }
}

/// <summary>The pharmacy report (§2.5).</summary>
public sealed class VisitReportPharmacy
{
    public List<VisitReportStockRow> Stock { get; set; } = new();
    public VisitReportShelf? Shelf { get; set; }
    public List<VisitReportSalesRow> Sales { get; set; } = new();
    public List<VisitReportCompetitor> Competitors { get; set; } = new();
    public bool NoCompetitor { get; set; }
    public VisitReportRecommendation? Recommendation { get; set; }
    public VisitReportTraining? Training { get; set; }
}

public sealed class VisitReportStockRow
{
    public Guid GskuId { get; set; }

    /// <summary><c>crm-pharmacy-stock-level</c>.</summary>
    public string? LevelCode { get; set; }

    public bool NearExpiry { get; set; }
    public bool ReturnIssue { get; set; }
}

public sealed class VisitReportShelf
{
    /// <summary><c>crm-shelf-position</c>.</summary>
    public string? PositionCode { get; set; }

    public bool MaterialStand { get; set; }

    /// <summary>A MOD-0262 evidence content id (W3 evidence pattern).</summary>
    public Guid? PhotoEvidenceId { get; set; }
}

public sealed class VisitReportSalesRow
{
    public Guid GskuId { get; set; }

    /// <summary><c>crm-sales-trend</c> (required).</summary>
    public string? TrendCode { get; set; }

    /// <summary>Optional (S3: a pharmacist's number is not reliable).</summary>
    public int? BoxesPerMonth { get; set; }
}

public sealed class VisitReportRecommendation
{
    /// <summary><c>crm-recommendation-level</c>.</summary>
    public string? LevelCode { get; set; }

    public List<Guid> LinkedContactIds { get; set; } = new();
}

public sealed class VisitReportTraining
{
    public bool? Given { get; set; }
    public List<VisitReportRequest> Requests { get; set; } = new();
}

/// <summary>Safety: the adverse event question on both kinds, the off-label request on a doctor report — answered, never defaulted.</summary>
public sealed class VisitReportSafety
{
    public bool? AdverseEvent { get; set; }
    public string? AdverseEventNote { get; set; }
    public bool? OffLabelRequest { get; set; }
}

public sealed class VisitReportFollowUp
{
    public bool Required { get; set; }
    public DateOnly? SuggestedDate { get; set; }
    public string? Note { get; set; }
}

/// <summary>One changed value of an amendment: the JSON path, the old and the new JSON (W4: the defect of storing only the
/// field name is closed).</summary>
public sealed class VisitReportAmendmentChange
{
    public string Path { get; set; } = string.Empty;
    public string? Old { get; set; }
    public string? New { get; set; }
}

public static class VisitReportSchemaVersions
{
    public const int V1 = 1;
    public const int V2 = 2;
}

/// <summary>The kind of a v2 report (from the planned visit's target).</summary>
public static class VisitReportKinds
{
    public const string Doctor = "doctor";
    public const string Pharmacy = "pharmacy";
}

/// <summary>An objection's resolution (in-domain).</summary>
public static class VisitReportObjectionResolutions
{
    public const string Yes = "yes";
    public const string Partly = "partly";
    public const string No = "no";

    public static readonly IReadOnlyList<string> All = new[] { Yes, Partly, No };
}

/// <summary>WP-VW-W4 (W4-BE-a, §2.1) — the ten report reference sets (tenant BRD, 7 languages).</summary>
public static class VisitReportV2ReferenceSets
{
    public const string VisitSuccess = "crm-visit-success";
    public const string DoctorCommitment = "crm-doctor-commitment";
    public const string VisitInterest = "crm-visit-interest";
    public const string VisitRequestType = "crm-visit-request-type";
    public const string CompetitorAdvantage = "crm-competitor-advantage";
    public const string PharmacyMetRole = "crm-pharmacy-met-role";
    public const string PharmacyStockLevel = "crm-pharmacy-stock-level";
    public const string ShelfPosition = "crm-shelf-position";
    public const string SalesTrend = "crm-sales-trend";
    public const string RecommendationLevel = "crm-recommendation-level";

    public static readonly IReadOnlyList<string> All = new[]
    {
        VisitSuccess, DoctorCommitment, VisitInterest, VisitRequestType, CompetitorAdvantage,
        PharmacyMetRole, PharmacyStockLevel, ShelfPosition, SalesTrend, RecommendationLevel
    };
}

public static class VisitReportV2Limits
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 240;
    public const int MaxNextMessage = 500;
    public const int MaxGeneralNote = 4000;
    public const int MaxNote = 2000;
    public const int MinSampleQuantity = 1;
    public const int MaxSampleQuantity = 100;
    public const int MaxRows = 50;
    public const int MaxNameLength = 200;
    public const int MinAggregateCell = 5;
}
