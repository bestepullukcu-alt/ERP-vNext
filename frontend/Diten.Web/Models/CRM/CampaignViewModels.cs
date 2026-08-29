using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

public sealed class CampaignEditViewModel : IValidatableObject
{
    public Guid? CampaignId { get; set; }

    /// <summary>
    /// FU10 — optional on create: leaving it empty asks the server for CMP-{YYYY}-{sequence}. Still editable, so a
    /// team with its own numbering can supply one. Immutable after create.
    /// </summary>
    [StringLength(100)]
    public string? CampaignCode { get; set; }

    [Required, StringLength(240)]
    public string CampaignName { get; set; } = string.Empty;

    [Required]
    public string CampaignType { get; set; } = string.Empty;

    [Required]
    public string CampaignStatus { get; set; } = "draft";

    public string? ObjectiveType { get; set; }
    [StringLength(160)] public string? BusinessUnitId { get; set; }
    public string? DefaultConsentChannel { get; set; }
    public string? DefaultConsentPurpose { get; set; }

    [Required]
    public DateTimeOffset? StartDate { get; set; }

    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// MOD-0165 FU08 — the planning period this campaign is pinned to, or null when it is not cycle-bound.
    /// Optional; the containment rule only exists while a value is present.
    /// </summary>
    public Guid? CyclePeriodId { get; set; }

    /// <summary>
    /// MOD-0165 FU10 — how the campaign is targeted: <c>segment</c> or <c>manual</c>. Required; an omitted value on
    /// an existing campaign keeps the mode it already has (a pre-FU10 row reads as manual).
    /// </summary>
    public string? TargetingMode { get; set; }

    /// <summary>
    /// FU10 — the segments targeted in <c>segment</c> mode. Dormant in manual mode: kept, not validated, not used.
    /// </summary>
    public List<Guid> TargetedSegmentIds { get; set; } = [];

    /// <summary>
    /// FU10 / AC-UI-3 — the segments the campaign is ALREADY linked to, as read from the API. The picker lists only
    /// ACTIVE segments, so one archived or superseded since would be missing from the list, post nothing and silently
    /// unlink itself. The form injects these instead.
    /// </summary>
    public List<CampaignTargetedSegmentViewModel> CurrentTargetedSegments { get; set; } = [];

    /// <summary>
    /// MOD-0165 FU09 — the campaign's address level. Omitted means "derive it": a business unit makes it
    /// business-unit, nothing makes it tenant — exactly what a pre-FU09 campaign already meant.
    /// </summary>
    public string? ScopeType { get; set; }

    /// <summary>FU09 — the country reference, when <see cref="ScopeType"/> is <c>country</c>.</summary>
    public string? CountryScope { get; set; }

    /// <summary>FU09 — the legal-entity reference, when <see cref="ScopeType"/> is <c>legal-entity</c>.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>
    /// FU09 — the country the author is filtering business units by. Informational: it narrows the picker and is
    /// never posted as the campaign's scope.
    /// </summary>
    public string? BusinessUnitCountryFilter { get; set; }

    /// <summary>
    /// FU08 / AC-UI-3 — the period the campaign is ALREADY bound to, as read from the API. The picker lists only
    /// ACTIVE periods, so a campaign bound to a period that has since CLOSED would find its current value missing
    /// from the list, post null and silently unbind itself. The form injects this option instead.
    /// <para>Display only: it is never posted back. The binding travels in <see cref="CyclePeriodId"/> alone.</para>
    /// </summary>
    public CampaignCyclePeriodViewModel? CurrentCyclePeriod { get; set; }
    [StringLength(4000)] public string? Description { get; set; }

    public IReadOnlyList<string> CampaignTypes { get; set; } = [];
    public IReadOnlyList<string> CampaignStatuses { get; set; } = [];
    public IReadOnlyList<string> ObjectiveTypes { get; set; } = [];
    public IReadOnlyList<string> ConsentChannels { get; set; } = [];
    public IReadOnlyList<string> ConsentPurposes { get; set; } = [];
    public string? ContractError { get; set; }
    public bool IsArchived { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate < StartDate)
        {
            yield return new ValidationResult("EndDateBeforeStartDate", [nameof(EndDate)]);
        }
    }
}

public sealed class CampaignExternalReferenceViewModel
{
    [StringLength(120)] public string SourceSystem { get; set; } = string.Empty;
    [StringLength(240)] public string ExternalId { get; set; } = string.Empty;
    [StringLength(240)] public string? ExternalCode { get; set; }
    [StringLength(400)] public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class CampaignPageViewModel
{
    public CampaignDetailViewModel Campaign { get; set; } = new();
    public CampaignContractViewModel Contract { get; set; } = new();
    public bool CanManageCampaign { get; set; }
    public bool CanReadTargets { get; set; }
    public bool CanManageTargets { get; set; }
    public bool CanCreateSnapshot { get; set; }
}

public sealed class CampaignDetailViewModel
{
    public Guid CampaignId { get; set; }
    public string CampaignCode { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string CampaignType { get; set; } = string.Empty;
    public string CampaignStatus { get; set; } = string.Empty;
    public string? ObjectiveType { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? DefaultConsentChannel { get; set; }
    public string? DefaultConsentPurpose { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public Guid? CyclePeriodId { get; set; }

    /// <summary>FU10 — the EFFECTIVE targeting mode (a pre-FU10 row reads as manual).</summary>
    public string TargetingMode { get; set; } = string.Empty;

    public List<Guid> TargetedSegmentIds { get; set; } = [];

    /// <summary>FU10 — read-time projection of the targeted segments. Never posted back, never stored.</summary>
    public List<CampaignTargetedSegmentViewModel> TargetedSegments { get; set; } = [];

    /// <summary>FU09 — the campaign's EFFECTIVE address level (derived for pre-FU09 rows).</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>FU09 — the derived second half of the address; null for the tenant scope.</summary>
    public string? ScopeRef { get; set; }

    public string? CountryScope { get; set; }

    public Guid? LegalEntityId { get; set; }

    /// <summary>FU08 — the bound period as the API projected it AT READ TIME. Never posted back and never stored;
    /// null when the campaign is unbound or the period could not be resolved.</summary>
    public CampaignCyclePeriodViewModel? CyclePeriod { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class CampaignListViewModel
{
    public List<CampaignDetailViewModel> Items { get; set; } = [];
    public int Total { get; set; }
}

public sealed class CampaignTargetViewModel
{
    public Guid CampaignTargetId { get; set; }
    public Guid CampaignId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string? TargetDisplayName { get; set; }
    public string TargetStatus { get; set; } = string.Empty;
    public string TargetSource { get; set; } = string.Empty;
    public string? SourceReferenceType { get; set; }
    public Guid? SourceReferenceId { get; set; }
    public Guid? SnapshotBatchId { get; set; }
    public string SelectionReason { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = [];
    public int? Priority { get; set; }
    public CampaignConsentEvaluationViewModel? ConsentEvaluation { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ExclusionReason { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class CampaignTargetListViewModel
{
    public List<CampaignTargetViewModel> Items { get; set; } = [];
    public int Total { get; set; }
}

public sealed class CampaignConsentEvaluationViewModel
{
    public string Decision { get; set; } = string.Empty;
    public string EligibilityStatus { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = [];
    public DateTimeOffset EvaluatedAt { get; set; }
    public Guid? MatchedConsentId { get; set; }
    public List<Guid> MatchedPreferenceIds { get; set; } = [];
    public string EvaluatorVersion { get; set; } = string.Empty;
    public string SelectionReason { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? Purpose { get; set; }
    public bool FilterApplied { get; set; }
}

public sealed class CampaignContractViewModel
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public CampaignFeatureFlagsViewModel Features { get; set; } = new();
    public CampaignVocabularyViewModel Vocabulary { get; set; } = new();
    public CampaignConsentIntegrationViewModel? ConsentIntegration { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class CampaignFeatureFlagsViewModel
{
    public bool SupportsCampaignManagement { get; set; }
    public bool SupportsCampaignTargetManagement { get; set; }
    public bool SupportsStaticTargetSnapshot { get; set; }
    public bool SupportsConsentEvaluationIntegration { get; set; }
    public bool SupportsTargetExclusionReason { get; set; }
    public bool SupportsTargetSourceProvenance { get; set; }
}

public sealed class CampaignVocabularyViewModel
{
    public List<string> CampaignTypes { get; set; } = [];
    public List<string> CampaignStatuses { get; set; } = [];
    public List<string> ObjectiveTypes { get; set; } = [];
    public List<string> TargetTypes { get; set; } = [];
    public List<string> TargetSources { get; set; } = [];
    public List<string> TargetStatuses { get; set; } = [];
    public List<string> SnapshotRowOutcomes { get; set; } = [];
    public List<string> ConsentChannels { get; set; } = [];
    public List<string> ConsentPurposes { get; set; } = [];
}

public sealed class CampaignConsentIntegrationViewModel
{
    public string ProviderModule { get; set; } = string.Empty;
    public string ProviderSeam { get; set; } = string.Empty;
    public string EvaluatorVersion { get; set; } = string.Empty;
    public List<string> EvaluableTargetTypes { get; set; } = [];
    public string MissingContextBehavior { get; set; } = string.Empty;
    public string BlockedBehavior { get; set; } = string.Empty;
    public string UnknownBehavior { get; set; } = string.Empty;
    public string FilterDisabledBehavior { get; set; } = string.Empty;
    public string NotApplicableBehavior { get; set; } = string.Empty;
}

public sealed class CampaignSnapshotResultViewModel
{
    public Guid SnapshotBatchId { get; set; }
    public Guid CampaignId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public bool ConsentFilterApplied { get; set; }
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int ReconciledCount { get; set; }
    public int ActiveCount { get; set; }
    public int ExcludedCount { get; set; }
    public int ConflictCount { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
}

public sealed class CampaignGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
