namespace Diten.MdmService.Domain.Entities;

// WP-VW-W4 (W4-BE-b2, §2.6, K-W6) — a COMPETITOR product: a separate MOD-0290 record, never a Global Product. It is not
// sold and never enters an order; the visit report picks it (W4-BE-a) and the market analysis counts it (W7).
//
// Lifecycle: proposed (a rep added it from a report: usable at once by its proposer only) · active · inactive (not
// selectable any more; old reports keep it) · merged (joined another record: MergedIntoId points at the survivor, the
// record is never deleted) · rejected (a proposal turned down: old reports keep the plain text). There is NO delete.
public sealed class CompetitorProduct : EntityBase
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The tenant-unique key (company + name, lower-case, Turkish folding, letters / digits only).</summary>
    public string NormalizedKey { get; set; } = string.Empty;

    /// <summary>Other names: spelling variants, old names, and the names of the records merged into this one.</summary>
    public List<string> Aliases { get; set; } = [];

    public string Company { get; set; } = string.Empty;

    /// <summary>Free tags (a dictionary may come later, K-W6): "amlodipine", "valsartan".</summary>
    public List<string> ActiveIngredients { get; set; } = [];

    /// <summary>The MDM dosage form vocabulary (BrandProductVocabulary.DosageForms, G4).</summary>
    public string? FormCode { get; set; }
    public string? Strength { get; set; }
    public string? AtcCode { get; set; }

    /// <summary>Our Global Products it competes with (at least one for an active record).</summary>
    public List<Guid> CompetesWithGlobalProductIds { get; set; } = [];

    /// <summary>CRM concept graph indication nodes — ids only, the names are read from the CRM (G3).</summary>
    public List<Guid> IndicationNodeIds { get; set; } = [];

    public string Status { get; set; } = CompetitorProductStatuses.Active;
    public Guid? MergedIntoId { get; set; }
    public DateTime? MergedAt { get; set; }
    public string? MergedBy { get; set; }

    /// <summary>The records merged INTO this one (the merge trace on the survivor).</summary>
    public List<CompetitorMergeTrace> MergedFrom { get; set; } = [];

    /// <summary>One current row per market (BRD <c>market</c>); a price change opens a new period, the old one goes to
    /// <see cref="CountryPriceHistory"/>.</summary>
    public List<CompetitorCountryPrice> CountryPrices { get; set; } = [];
    public List<CompetitorPriceHistoryEntry> CountryPriceHistory { get; set; } = [];

    // ── proposal (status proposed) ──
    public string? ProposedBy { get; set; }
    public DateTime? ProposedAt { get; set; }
    public Guid? ProposedFromReportId { get; set; }
    public Guid? ProposedForProductId { get; set; }
    public string? RejectedReason { get; set; }

    public DateTime? DeactivatedAt { get; set; }
    public string? DeactivatedBy { get; set; }

    public List<CompetitorChange> ChangeLog { get; set; } = [];

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Selectable in a new report / as a merge target (an inactive / merged / rejected record never is).</summary>
    public bool IsSelectable => Status == CompetitorProductStatuses.Active && !IsDeleted;
}

public static class CompetitorProductStatuses
{
    public const string Proposed = "proposed";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Merged = "merged";
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All = [Proposed, Active, Inactive, Merged, Rejected];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value.Trim().ToLowerInvariant());
}

/// <summary>One market's current price and reimbursement (dates are UTC calendar days — DateTime, never DateTimeOffset,
/// so no parallel-array sort trap).</summary>
public sealed class CompetitorCountryPrice
{
    public string MarketCode { get; set; } = string.Empty;
    public decimal BoxPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>A value of the tenant BRD set <c>mdm-reimbursement-status</c>.</summary>
    public string ReimbursementStatusCode { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public string? Source { get; set; }
}

/// <summary>A closed price period (read only).</summary>
public sealed class CompetitorPriceHistoryEntry
{
    public string MarketCode { get; set; } = string.Empty;
    public decimal BoxPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string ReimbursementStatusCode { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? Source { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }

    /// <summary><c>changed</c> (a new period replaced it) | <c>removed</c> (the market row was taken out) | <c>moved</c>
    /// (a merge carried the row to the survivor).</summary>
    public string Reason { get; set; } = "changed";
}

public sealed class CompetitorMergeTrace
{
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceCompany { get; set; }
    public string? ProposedBy { get; set; }
    public DateTime? ProposedAt { get; set; }
    public DateTime MergedAt { get; set; }
    public string? MergedBy { get; set; }

    /// <summary><c>proposal</c> (a rep proposal, via the approval workflow) | <c>record</c> (two active records, direct).</summary>
    public string Kind { get; set; } = "proposal";
    public List<string> MovedMarketCodes { get; set; } = [];
}

/// <summary>One audited change (who, when, field, old → new).</summary>
public sealed class CompetitorChange
{
    public DateTime At { get; set; }
    public string? By { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
