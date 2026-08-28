namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 FU03 assignment rule (aggregate, model-scoped, pack §7.3). Declares how account candidates are matched
/// to ONE <see cref="TerritoryId"/> node inside <see cref="ModelId"/>.
///
/// <para><b>A rule never assigns anything.</b> It is an input to the side-effect-free FU03 preview; persisting
/// <c>AccountTerritoryAssignment</c> rows is FU05 and does not exist yet. Nothing here writes to the MOD-0149
/// Account master either — accounts are read-only inputs (pack §11.1).</para>
/// </summary>
public sealed class TerritoryAssignmentRule : EntityBase
{
    public Guid ModelId { get; set; }

    /// <summary>Target <see cref="TerritoryNode"/> inside the same model. Matched accounts become candidates for it.</summary>
    public Guid TerritoryId { get; set; }

    /// <summary>Human-readable code, unique within (TenantId, ModelId). Trimmed/normalized.</summary>
    public string RuleCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>MOD-0048 published <c>territory-rule-type</c> value code. FU03 evaluates a subset (see
    /// <c>TerritoryRuleTypes.Fu03Supported</c>); every other published type is rejected with a controlled 400.</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>MOD-0048 published <c>territory-conflict-policy</c> value code. Surfaced by preview; enforcement
    /// (blocking an apply) belongs to FU05/FU06.</summary>
    public string ConflictPolicy { get; set; } = string.Empty;

    /// <summary>Lower value wins. Used to pick the winning rule when an account matches several rules (pack §7.3).</summary>
    public int Priority { get; set; }

    /// <summary>Disabled rules are kept but skipped by preview.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Typed, whitelisted match criteria — never free-form JSON.</summary>
    public TerritoryRuleCriteria Criteria { get; set; } = new();

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public string? CorrelationId { get; set; }
}

/// <summary>
/// FU03 match criteria (value object). Every field is an explicit whitelist entry — there is no free-form expression
/// and no arbitrary JSON, so a payload naming an unknown field fails validation instead of being silently ignored.
///
/// <para>Within one field the values are OR-ed; across fields they are AND-ed. Empty/absent field = not constrained.
/// All account attributes are read from the MOD-0149 Account read model; MOD-0151 stores none of them.</para>
/// </summary>
public sealed class TerritoryRuleCriteria
{
    /// <summary>Account <c>CountryRef</c> codes (case-insensitive).</summary>
    public List<string> CountryRefs { get; set; } = new();

    /// <summary>Account <c>CityRef</c> codes (case-insensitive).</summary>
    public List<string> CityRefs { get; set; } = new();

    /// <summary>Account <c>DistrictRef</c> codes (case-insensitive).</summary>
    public List<string> DistrictRefs { get; set; } = new();

    /// <summary>MOD-0048 <c>account-type</c> value codes.</summary>
    public List<string> AccountTypes { get; set; } = new();

    /// <summary>MOD-0048 <c>account-category</c> value codes.</summary>
    public List<string> AccountCategories { get; set; } = new();

    /// <summary>MOD-0048 <c>account-status</c> value codes.</summary>
    public List<string> AccountStatuses { get; set; } = new();

    /// <summary>Explicit account ids to include (the <c>account-list</c> rule type is built on this).</summary>
    public List<Guid> IncludeAccountIds { get; set; } = new();

    /// <summary>Explicit account ids to exclude. Applied last and always wins over a match.</summary>
    public List<Guid> ExcludeAccountIds { get; set; } = new();

    public bool IsEmpty
        => CountryRefs.Count == 0 && CityRefs.Count == 0 && DistrictRefs.Count == 0
           && AccountTypes.Count == 0 && AccountCategories.Count == 0 && AccountStatuses.Count == 0
           && IncludeAccountIds.Count == 0 && ExcludeAccountIds.Count == 0;
}
