namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.3: an MF / discount / quantity guardrail of one legal entity × SKU × (optional segment /
/// period), versioned. A <see cref="RuleKey"/> keeps its identity across versions; a change (create / revise / retire) is a
/// new version that goes through four eyes (D13) and, when published, supersedes (or retires) the previous active one.
/// The first release is segment-less (§20.3 AÇIK-3): <see cref="SegmentCode"/> is carried, evaluation ignores segment rules.
/// </summary>
public sealed class PricingRule : EntityBase, IWorkflowApprovable
{
    public Guid RuleKey { get; set; }
    public int RuleVersion { get; set; } = 1;

    public Guid LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }

    public Guid SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? SkuCode { get; set; }
    public string? SkuName { get; set; }

    /// <summary>BRD <c>pharmacy-segment</c> value code (D8); null = every pharmacy.</summary>
    public string? SegmentCode { get; set; }

    /// <summary>The "period" (D8): both null = without end.</summary>
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public PricingPctLimit MaxDiscountPct { get; set; } = new();
    public PricingMfLimit MaxMf { get; set; } = new();
    public PricingPctLimit MaxCombinedPct { get; set; } = new();
    public PricingQuantityLimit Quantity { get; set; } = new();

    /// <summary><see cref="PricingRuleStatuses"/>.</summary>
    public string Status { get; set; } = PricingRuleStatuses.Draft;

    /// <summary><see cref="PricingRuleChangeKinds"/> — what this version asks the reviewer to approve.</summary>
    public string ChangeKind { get; set; } = PricingRuleChangeKinds.Create;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }

    public List<ClaimReviewRound> ReviewRounds { get; set; } = new();
    public List<RegulatoryTextDecision> Decisions { get; set; } = new();

    public bool IsDraft() => Status == PricingRuleStatuses.Draft;
    public bool IsActive() => Status == PricingRuleStatuses.Active;
}

/// <summary>A soft / hard pair of percentages (0–100, one decimal; Soft ≤ Hard).</summary>
public sealed class PricingPctLimit
{
    public decimal? Soft { get; set; }
    public decimal? Hard { get; set; }

    public bool IsSet() => Soft is not null || Hard is not null;
}

/// <summary>"10+2": buy <see cref="Buy"/>, get <see cref="Free"/> free; ratio = Free / (Buy + Free).</summary>
public sealed class PricingMfTerm
{
    public int Buy { get; set; }
    public int Free { get; set; }
}

public sealed class PricingMfLimit
{
    public PricingMfTerm? Soft { get; set; }
    public PricingMfTerm? Hard { get; set; }

    public bool IsSet() => Soft is not null || Hard is not null;
}

public sealed class PricingQuantityLimit
{
    public int? Min { get; set; }
    public int? SoftMax { get; set; }
    public int? HardMax { get; set; }

    public bool IsSet() => Min is not null || SoftMax is not null || HardMax is not null;
}

public static class PricingRuleStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Active = "active";
    public const string Superseded = "superseded";
    public const string Retired = "retired";
    public const string Discarded = "discarded";

    public static readonly IReadOnlyList<string> All = new[] { Draft, InReview, Active, Superseded, Retired, Discarded };
}

public static class PricingRuleChangeKinds
{
    public const string Create = "create";
    public const string Revise = "revise";
    public const string Retire = "retire";

    public static readonly IReadOnlyList<string> All = new[] { Create, Revise, Retire };
}
