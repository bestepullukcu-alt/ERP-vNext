namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2, K-W5) — what the field knows about a DOCTOR (a CRM Contact), kept apart from the Contact
/// master: the adoption typology, the communication style, the KOL level (codes of tenant BRD sets) and the doctor's
/// estimated monthly patients (asked every three months). One document per (TenantId, ContactId).
/// <para>The best day / time and the preferred channel are NOT copied here: <see cref="ContactAvailability"/> and the
/// <see cref="PreferenceRecord"/>s (<c>preferred-channel</c>, <c>preferred-visit-window</c>) stay their owners and are
/// read next to the profile.</para>
/// <para>Every change appends a <see cref="DoctorProfileChange"/> (field, old, new, who, when, source).</para>
/// </summary>
public sealed class DoctorProfile : EntityBase
{
    public Guid ContactId { get; set; }

    /// <summary><c>crm-adoption-typology</c> code.</summary>
    public string? AdoptionTypologyCode { get; set; }

    /// <summary><c>crm-communication-style</c> code.</summary>
    public string? CommunicationStyleCode { get; set; }

    /// <summary><c>crm-kol-level</c> code.</summary>
    public string? KolLevelCode { get; set; }

    /// <summary>The doctor's estimated monthly patients (all indications), 0–100 000.</summary>
    public int? TotalMonthlyPatients { get; set; }

    /// <summary>When <see cref="TotalMonthlyPatients"/> was last given (the three-month rule).</summary>
    public DateTimeOffset? TotalPatientsAsOf { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary>WP-VW-W4 (W4-BE-c) — one APPEND-ONLY row of a doctor profile's history.</summary>
public sealed class DoctorProfileChange : EntityBase
{
    public Guid ContactId { get; set; }

    /// <summary>The profile field (camelCase, as on the wire).</summary>
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? By { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary><c>manual</c> | <c>report:{visitReportId}</c>.</summary>
    public string Source { get; set; } = DoctorInsightsSources.Manual;
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2, K-W4) — one doctor's potential for ONE of our products (MDM Global Product): estimated
/// monthly patients per indication × patient profile (Concept Graph nodes), or "sees no patients in these indications",
/// and our share bucket (<c>crm-share-bucket</c>). One document per (TenantId, ContactId, GlobalProductId).
/// <para>The potential class and the opportunity are COMPUTED on read (thresholds × §2.3 matrix), never stored.</para>
/// </summary>
public sealed class DoctorProductPotential : EntityBase
{
    public Guid ContactId { get; set; }
    public Guid GlobalProductId { get; set; }

    public List<DoctorPotentialRow> IndicationRows { get; set; } = new();

    public bool NoPatientsInIndications { get; set; }

    /// <summary>When the patient numbers were last given (the three-month rule); null = never.</summary>
    public DateTimeOffset? AsOf { get; set; }

    /// <summary><c>crm-share-bucket</c> code.</summary>
    public string? ShareBucketCode { get; set; }

    public DateTimeOffset? ShareAsOf { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>The monthly patients across the indications (0 when "no patients"); null when nothing was given.</summary>
    public int? IndicationTotal()
        => NoPatientsInIndications ? 0
            : IndicationRows.Count == 0 ? null
            : IndicationRows.Sum(r => r.MonthlyPatients);
}

/// <summary>One indication (× optional patient profile) row of a potential.</summary>
public sealed class DoctorPotentialRow
{
    public Guid IndicationNodeId { get; set; }
    public Guid? ProfileNodeId { get; set; }
    public int MonthlyPatients { get; set; }
}

/// <summary>WP-VW-W4 (W4-BE-c) — one APPEND-ONLY row of a product potential's history (one per changed row / field).</summary>
public sealed class DoctorProductPotentialChange : EntityBase
{
    public Guid ContactId { get; set; }
    public Guid GlobalProductId { get; set; }

    /// <summary><c>row:{indicationNodeId}[/{profileNodeId}]</c> | <c>noPatientsInIndications</c> | <c>shareBucketCode</c>.</summary>
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? By { get; set; }
    public DateTimeOffset At { get; set; }
    public string Source { get; set; } = DoctorInsightsSources.Manual;
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2, K-W4) — the Low / Medium / High boundaries of a product's potential, set by marketing:
/// a total below <see cref="LowBelow"/> is <c>low</c>, above <see cref="HighAbove"/> is <c>high</c>, else <c>medium</c>.
/// <see cref="GlobalProductId"/> null = the tenant default; no document = 10 / 30. Rule: 0 &lt; LowBelow ≤ HighAbove.
/// </summary>
public sealed class ProductPotentialThreshold : EntityBase
{
    public Guid? GlobalProductId { get; set; }
    public int LowBelow { get; set; } = ProductPotentialThresholdDefaults.LowBelow;
    public int HighAbove { get; set; } = ProductPotentialThresholdDefaults.HighAbove;
    public string? UpdatedBy { get; set; }
}

/// <summary>WP-VW-W4 (W4-BE-c) — one APPEND-ONLY row of the thresholds' history.</summary>
public sealed class ProductPotentialThresholdChange : EntityBase
{
    /// <summary>Null = the tenant default.</summary>
    public Guid? GlobalProductId { get; set; }

    /// <summary><c>lowBelow</c> | <c>highAbove</c>.</summary>
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? By { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class ProductPotentialThresholdDefaults
{
    public const int LowBelow = 10;
    public const int HighAbove = 30;
}

/// <summary>WP-VW-W4 (W4-BE-c, §2.1) — the five profile / potential reference sets (tenant BRD, 7 languages).</summary>
public static class DoctorInsightsReferenceSets
{
    public const string AdoptionStage = "crm-adoption-stage";
    public const string ShareBucket = "crm-share-bucket";
    public const string AdoptionTypology = "crm-adoption-typology";
    public const string CommunicationStyle = "crm-communication-style";
    public const string KolLevel = "crm-kol-level";

    public static readonly IReadOnlyList<string> All = new[] { AdoptionStage, ShareBucket, AdoptionTypology, CommunicationStyle, KolLevel };
}

/// <summary>The share buckets (the §2.3 matrix columns; the codes of <c>crm-share-bucket</c>).</summary>
public static class ShareBuckets
{
    public const string None = "none";
    public const string Occasional = "occasional";
    public const string Half = "half";
    public const string Mostly = "mostly";
    public const string Always = "always";

    public static readonly IReadOnlyList<string> All = new[] { None, Occasional, Half, Mostly, Always };
}

public static class PotentialClasses
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static readonly IReadOnlyList<string> All = new[] { Low, Medium, High };
}

public static class OpportunityCodes
{
    public const string Opportunity = "opportunity";
    public const string Grow = "grow";
    public const string Keep = "keep";
    public const string LowPriority = "low_priority";

    public static readonly IReadOnlyList<string> All = new[] { Opportunity, Grow, Keep, LowPriority };
}

public static class DoctorInsightsSources
{
    public const string Manual = "manual";
    public const string ReportPrefix = "report:";

    public static string Report(Guid visitReportId) => ReportPrefix + visitReportId.ToString("D");
}

public static class DoctorInsightsLimits
{
    public const int MaxMonthlyPatients = 100_000;
    public const int MaxIndicationRows = 50;
    public const int DueAfterDays = 90;

    /// <summary>An aggregate cell counting fewer doctors than this is hidden (<c>"&lt;5"</c>).</summary>
    public const int MinAggregateCell = 5;
}

/// <summary>The coded failures of the doctor insights endpoints (<c>[message, code]</c>).</summary>
public static class DoctorInsightsErrors
{
    public const string Invalid = "doctor_insights_invalid";
    public const string PotentialExceedsTotal = "potential_exceeds_total";
    public const string ThresholdInvalid = "potential_threshold_invalid";
    public const string ProductInvalid = "doctor_insights_product_invalid";
    public const string CodeInvalid = "doctor_insights_code_invalid";
    public const string NotFound = "doctor_not_found";
    public const string ReferenceDataUnavailable = "reference_data_unavailable";
    public const string VersionConflict = "version_conflict";
    public const string NoConceptGraph = "no_concept_graph";
    public const string NoIndications = "no_indications";
}
