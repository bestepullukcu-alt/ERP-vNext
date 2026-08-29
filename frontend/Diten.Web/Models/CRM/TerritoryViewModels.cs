using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// ============================================================================================================
// MOD-0151 FU02 — Territory Hierarchy UI / Territory Model Viewer view models (Diten.Web tenant shell).
// Backend is FU01 only (TerritoryModel + TerritoryNode). All traffic goes through the Gateway (5000); CrmService
// (5061) is never called directly. Reference dropdowns come from MOD-0048 published values — no local fallback.
// TenantId is NEVER a form field and is NEVER posted (resolved server-side from the JWT / X-Tenant-Id header).
// ============================================================================================================

// ---- Contract ----

public sealed class TerritoryContractViewModel
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string RuntimeScope { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public bool IsReady { get; set; }
    public TerritoryFeatureFlagsViewModel Features { get; set; } = new();
    public List<TerritoryReferenceSetReadinessViewModel> RequiredReferenceSets { get; set; } = [];
    public List<string> MissingRequiredReferenceSets { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class TerritoryFeatureFlagsViewModel
{
    public bool TerritoryModels { get; set; }
    public bool TerritoryNodes { get; set; }
    public bool AssignmentRules { get; set; }
    public bool AccountAssignmentApply { get; set; }
    public bool ResourceAssignments { get; set; }
    public bool WorkflowActivation { get; set; }
    public bool EvidencePack { get; set; }
    public bool ImportExport { get; set; }
    public bool UiEnabled { get; set; }
    public bool SupportsLifecycleActions { get; set; }
    public bool SupportsComputedExpiry { get; set; }
    public bool SupportsDraftSoftDelete { get; set; }
    public bool SupportsWorkflowActivation { get; set; }
    public bool SupportsApprovalTrace { get; set; }
    public bool SupportsAssignmentRules { get; set; }
    public bool SupportsAssignmentPreview { get; set; }
    public bool SupportsAccountAssignmentApply { get; set; }
    public bool SupportsAssignmentHistory { get; set; }
    public bool SupportsCoverageSummary { get; set; }
    /// <summary>MOD-0151 FU05A — current coverage only projects through an active territory model.</summary>
    public bool SupportsCoverageSummaryModelLifecycleGuard { get; set; }
    public bool SupportsResourceAssignments { get; set; }
    public bool SupportsResourceAssignmentLifecycle { get; set; }
    public bool SupportsResourceReplacement { get; set; }
    public bool SupportsResourceTransfer { get; set; }
    public bool SupportsCurrentResponsibility { get; set; }
    public bool SupportsPositionBasedResourceAssignment { get; set; }
}

public sealed class AccountTerritoryAssignmentListViewModel
{
    public int TotalCount { get; set; }
    public List<AccountTerritoryAssignmentViewModel> Items { get; set; } = [];
}

public sealed class AccountTerritoryAssignmentViewModel
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountDisplayName { get; set; } = string.Empty;
    public Guid TerritoryNodeId { get; set; }
    public string TerritoryNodeCode { get; set; } = string.Empty;
    public string TerritoryNodeName { get; set; } = string.Empty;
    public List<TerritoryBusinessScopeView> BusinessScopes { get; set; } = [];
    public string AssignmentSource { get; set; } = string.Empty;
    public string AssignmentStatus { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? AppliedRuleCode { get; set; }
}

public sealed class AccountAssignmentApplyForm
{
    public Guid? PreviewRunId { get; set; }
    public string SelectedRowsJson { get; set; } = "[]";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string ConflictPolicy { get; set; } = "block";
    public bool Override { get; set; }
    public string? OverrideReason { get; set; }
}

public sealed class TerritoryReferenceSetReadinessViewModel
{
    public string SetCode { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool Ready { get; set; }
    public int ExpectedValueCount { get; set; }
    public int ActualValueCount { get; set; }
    public bool MetadataReady { get; set; }
    public List<string> MissingMetadata { get; set; } = [];
}

// ---- TerritoryModel ----

public sealed class TerritoryModelListItemViewModel
{
    public Guid Id { get; set; }
    public string ModelCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StoredStatus { get; set; } = string.Empty;
    public string ComputedStatus { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public int VersionNumber { get; set; }
    public string? CountryScope { get; set; }
    public string? DivisionScope { get; set; }
    public List<TerritoryBusinessScopeView> BusinessScopes { get; set; } = [];
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TerritoryModelListViewModel
{
    public List<TerritoryModelListItemViewModel> Items { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class TerritoryModelDetailViewModel
{
    public Guid Id { get; set; }
    public string ModelCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StoredStatus { get; set; } = string.Empty;
    public string ComputedStatus { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public int VersionNumber { get; set; }
    public string? CountryScope { get; set; }
    public string? DivisionScope { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid? BasedOnModelId { get; set; }
    public string? ChangeReason { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    // FU02A: crossing business scopes persisted by the backend (business-unit only for now).
    public List<TerritoryBusinessScopeView> BusinessScopes { get; set; } = [];
}

/// <summary>FU02A business scope (scopeType + scopeCode) as returned by the Gateway detail response.</summary>
public sealed record TerritoryBusinessScopeView(string ScopeType, string ScopeCode);

/// <summary>Landing page: contract readiness + model list + permission-aware actions.</summary>
public sealed class TerritoryIndexPageViewModel
{
    public TerritoryContractViewModel? Contract { get; set; }
    public List<TerritoryModelListItemViewModel> Models { get; set; } = [];
    public bool ModelsUnavailable { get; set; }
    public bool CanManageModel { get; set; }
    public bool CanReadNode { get; set; }
}

public sealed class TerritoryModelEditViewModel
{
    public Guid? Id { get; set; }
    public Guid? BasedOnModelId { get; set; }

    [Required, StringLength(100)]
    public string ModelCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    // FU02A: CountryScope now carries a MOD-0048 published country VALUE CODE (single select), not free text.
    [StringLength(100)]
    public string? CountryScope { get; set; }

    // FU02A: legacy free-text "Division Scope" is retired from the UI. Kept only so an older payload still binds;
    // it is never surfaced as a field and never sent to the Gateway (superseded by BusinessUnitScopes below).
    [StringLength(100)]
    public string? DivisionScope { get; set; }

    // FU02A: Business Unit Scope — multi select of MOD-0048 published business-unit VALUE CODES (e.g. alpha, beta).
    // Reference-data driven; NO hardcoded fallback. Serialized to the Gateway as businessScopes[{scopeType,scopeCode}]
    // with scopeType fixed to "business-unit" (brand-group / product-portfolio are intentionally NOT sent here).
    public List<string> BusinessUnitScopes { get; set; } = [];

    [Required]
    [DataType(DataType.Date)]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    public DateTime? EffectiveTo { get; set; }

    [StringLength(1000)]
    public string? ChangeReason { get; set; }

    public bool IsEdit => Id.HasValue;

    // FU02A option sources for the server-rendered ModelForm (offcanvas loads them via /Models/lookups instead).
    // Populated from MOD-0048 published-values in the controller — never a hardcoded fallback.
    public IReadOnlyList<ReferenceOptionViewModel> CountryOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> BusinessUnitOptions { get; set; } = [];
    public bool CountryReferenceReady => CountryOptions.Count > 0;
    public bool BusinessUnitReferenceReady => BusinessUnitOptions.Count > 0;
}

// ---- TerritoryNode ----

public sealed class MicroZoneProfileViewModel
{
    public Guid? AnchorAccountId { get; set; }
    public string? ClusterNotes { get; set; }
    public string? PlanningCenterType { get; set; }
}

public sealed class TerritoryNodeViewModel
{
    public Guid Id { get; set; }
    public Guid ModelId { get; set; }
    public Guid? ParentTerritoryId { get; set; }
    public string TerritoryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TerritoryLevel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StoredStatus { get; set; } = string.Empty;
    public string ComputedStatus { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public string? CountryCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? RegionCode { get; set; }
    public string? AreaCode { get; set; }
    public string? ZoneCode { get; set; }
    public string? MicroZoneCode { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public int SortOrder { get; set; }
    public MicroZoneProfileViewModel? MicroZoneProfile { get; set; }
    public string? CorrelationId { get; set; }

    // Derived depth for the indented tree render (populated in the controller).
    public int Depth { get; set; }
}

public sealed class TerritoryHierarchyViewModel
{
    public Guid ModelId { get; set; }
    public List<TerritoryNodeViewModel> Nodes { get; set; } = [];
}

/// <summary>Details page: model header + FU01 limitation notes + hierarchy viewer + (permission-gated) node form.</summary>
public class TerritoryModelDetailPageViewModel
{
    public TerritoryModelDetailViewModel Model { get; set; } = new();
    public List<TerritoryNodeViewModel> Nodes { get; set; } = [];
    public bool NodesUnavailable { get; set; }
    public bool CanManageModel { get; set; }
    public bool CanManageNode { get; set; }
    public TerritoryContractViewModel? Contract { get; set; }
    public IReadOnlyList<ReferenceOptionViewModel> NodeLevelOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> AnchorAccountOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> PlanningCenterTypeOptions { get; set; } = [];
}

public sealed class TerritoryNodeEditViewModel
{
    public Guid ModelId { get; set; }
    public Guid? Id { get; set; }

    public Guid? ParentTerritoryId { get; set; }

    [Required, StringLength(100)]
    public string TerritoryCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string TerritoryLevel { get; set; } = string.Empty;

    [StringLength(50)] public string? CountryCode { get; set; }
    [StringLength(50)] public string? DivisionCode { get; set; }
    [StringLength(50)] public string? RegionCode { get; set; }
    [StringLength(50)] public string? AreaCode { get; set; }
    [StringLength(50)] public string? ZoneCode { get; set; }
    [StringLength(50)] public string? MicroZoneCode { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    public DateTime? EffectiveTo { get; set; }

    public int SortOrder { get; set; }

    // MicroZoneProfile — only sent when TerritoryLevel == "microzone".
    public Guid? AnchorAccountId { get; set; }
    [StringLength(1000)] public string? ClusterNotes { get; set; }
    [StringLength(100)] public string? PlanningCenterType { get; set; }

    public bool IsEdit => Id.HasValue;

    // Options (from MOD-0048 published values + FU01 node list) — never a local fallback.
    public IReadOnlyList<ReferenceOptionViewModel> LevelOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> ParentOptions { get; set; } = [];
    public string? ReferenceDependencyMessage { get; set; }
}

// ---- payloads sent to the Gateway (camelCase; NO TenantId) ----

public sealed class TerritoryModelSavePayload
{
    public string ModelCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CountryScope { get; set; }
    public string? DivisionScope { get; set; }
    public Guid? BasedOnModelId { get; set; }

    // FU02A contract: businessScopes is a passive list of {scopeType,scopeCode} value objects. The Gateway/CRM
    // backend currently ignores unknown members (System.Text.Json default = skip), so this round-trips as a no-op
    // until the backend BusinessScopes mini-FU lands. NEVER emitted with a brand/product scopeType from this form.
    public List<TerritoryBusinessScopePayload>? BusinessScopes { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ChangeReason { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>FU02A passive business-scope selection. ScopeType is fixed to <c>business-unit</c> for the Model form;
/// brand-group / product-portfolio are out of scope (separate later FU).</summary>
public sealed record TerritoryBusinessScopePayload(string ScopeType, string ScopeCode);

public sealed class MicroZoneProfileInputPayload
{
    public Guid? AnchorAccountId { get; set; }
    public string? ClusterNotes { get; set; }
    public string? PlanningCenterType { get; set; }
}

public sealed class TerritoryNodeSavePayload
{
    public Guid? ParentTerritoryId { get; set; }
    public string TerritoryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TerritoryLevel { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? RegionCode { get; set; }
    public string? AreaCode { get; set; }
    public string? ZoneCode { get; set; }
    public string? MicroZoneCode { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public int SortOrder { get; set; }
    public MicroZoneProfileInputPayload? MicroZoneProfile { get; set; }
    public string? CorrelationId { get; set; }
}

// ===================================================================================================
// FU03 — assignment rules + preview
//
// Read/preview view models plus a rule save payload. There is deliberately NO "apply" payload: turning a
// preview into real AccountTerritoryAssignment rows is FU05 and has no endpoint on either side yet.
// ===================================================================================================

/// <summary>Page context for the Preview and History screens. <see cref="Rule"/> is null when the screen is showing
/// the whole model rather than a single rule.</summary>
public sealed class TerritoryRuleScopedPageViewModel : TerritoryModelDetailPageViewModel
{
    public TerritoryAssignmentRuleViewModel? Rule { get; set; }

    public bool IsRuleScoped => Rule is not null;
}

public sealed class TerritoryAssignmentRuleListViewModel
{
    public Guid ModelId { get; set; }
    public string ModelStatus { get; set; } = string.Empty;
    public bool IsEditable { get; set; }
    public int TotalCount { get; set; }
    public int EnabledCount { get; set; }
    public List<TerritoryAssignmentRuleViewModel> Items { get; set; } = [];
}

public sealed class TerritoryAssignmentRuleViewModel
{
    public Guid Id { get; set; }
    public Guid ModelId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid TerritoryId { get; set; }
    public string? TerritoryCode { get; set; }
    public string? TerritoryName { get; set; }
    public string? TerritoryLevel { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string ConflictPolicy { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public TerritoryRuleCriteriaViewModel Criteria { get; set; } = new();
    public string CriteriaSummary { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
}

public sealed class TerritoryRuleCriteriaViewModel
{
    public List<string> CountryRefs { get; set; } = [];
    public List<string> CityRefs { get; set; } = [];
    public List<string> DistrictRefs { get; set; } = [];
    public List<string> AccountTypes { get; set; } = [];
    public List<string> AccountCategories { get; set; } = [];
    public List<string> AccountStatuses { get; set; } = [];
    public List<Guid> IncludeAccountIds { get; set; } = [];
    public List<Guid> ExcludeAccountIds { get; set; } = [];
}

public sealed class TerritoryAssignmentPreviewViewModel
{
    public Guid ModelId { get; set; }
    public string ModelStatus { get; set; } = string.Empty;
    public Guid PreviewRunId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>The instant the rule effective-windows were evaluated against (clamped into the model window for a
    /// future- or past-dated model).</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Always false — the backend contract guarantees preview persists nothing.</summary>
    public bool PersistedAssignments { get; set; }

    public int EvaluatedRuleCount { get; set; }
    public int SkippedRuleCount { get; set; }
    public long TotalTenantAccounts { get; set; }
    public int ScannedAccounts { get; set; }
    public int TotalCandidateAccounts { get; set; }
    public int UnmatchedAccountsCount { get; set; }
    public int ConflictCount { get; set; }
    public List<TerritoryAssignmentPreviewMatchViewModel> MatchedAccounts { get; set; } = [];
    public List<TerritoryAssignmentPreviewConflictViewModel> Conflicts { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<TerritoryAssignmentPreviewRuleSummaryViewModel> CriteriaSummary { get; set; } = [];
}

public sealed class TerritoryAssignmentPreviewMatchViewModel
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid TargetTerritoryNodeId { get; set; }
    public string? TargetTerritoryCode { get; set; }
    public string? TargetTerritoryName { get; set; }
    public string? TargetTerritoryLevel { get; set; }
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public string ConflictStatus { get; set; } = string.Empty;
}

public sealed class TerritoryAssignmentPreviewConflictViewModel
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public List<TerritoryAssignmentPreviewCandidateViewModel> CandidateTerritoryNodes { get; set; } = [];
    public List<Guid> ConflictingRuleIds { get; set; } = [];
    public string ConflictPolicy { get; set; } = string.Empty;
    public string ResolutionSuggestion { get; set; } = string.Empty;
}

public sealed class TerritoryAssignmentPreviewCandidateViewModel
{
    public Guid TerritoryNodeId { get; set; }
    public string? TerritoryCode { get; set; }
    public string? TerritoryName { get; set; }
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsWinner { get; set; }
}

public sealed class TerritoryAssignmentPreviewRuleSummaryViewModel
{
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public bool Evaluated { get; set; }
    public string? SkipReason { get; set; }
    public string CriteriaSummary { get; set; } = string.Empty;
    public int MatchCount { get; set; }
}

/// <summary>Assignment-rule create/edit form model. Criteria are typed as comma-separated codes and split
/// server-side into the typed whitelist the backend expects — the form never posts a free-form expression.</summary>
public sealed class TerritoryAssignmentRuleEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string RuleCode { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid TerritoryId { get; set; }

    [Required]
    public string RuleType { get; set; } = string.Empty;

    [Required]
    public string ConflictPolicy { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int Priority { get; set; } = 100;

    public bool IsEnabled { get; set; } = true;

    // Criteria are multi-select lists bound from MOD-0048 published values — never free text. Values within one
    // field are OR-ed by the backend matcher, different fields are AND-ed.
    public List<string>? CountryRefs { get; set; }
    public List<string>? CityRefs { get; set; }
    public List<string>? DistrictRefs { get; set; }
    public List<string>? AccountTypes { get; set; }
    public List<string>? AccountCategories { get; set; }
    public List<string>? AccountStatuses { get; set; }

    [DataType(DataType.Date)]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    public DateTime? EffectiveTo { get; set; }
}

public sealed class TerritoryAssignmentRuleSavePayload
{
    public string RuleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid TerritoryId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string ConflictPolicy { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public TerritoryRuleCriteriaPayload Criteria { get; set; } = new();
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class TerritoryRuleCriteriaPayload
{
    public List<string> CountryRefs { get; set; } = [];
    public List<string> CityRefs { get; set; } = [];
    public List<string> DistrictRefs { get; set; } = [];
    public List<string> AccountTypes { get; set; } = [];
    public List<string> AccountCategories { get; set; } = [];
    public List<string> AccountStatuses { get; set; } = [];
    public List<Guid> IncludeAccountIds { get; set; } = [];
    public List<Guid> ExcludeAccountIds { get; set; } = [];
}

// ===================================================================================================
// FU04 — resource (people) assignments
//
// Assigning a PERSON to a territory node. Nothing here assigns an ACCOUNT — that is FU05 and has no
// payload, endpoint or aggregate yet.
// ===================================================================================================

public sealed class TerritoryResourceAssignmentListViewModel
{
    public Guid ModelId { get; set; }
    public string ModelStatus { get; set; } = string.Empty;
    public bool IsEditable { get; set; }
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public List<string> ModelBusinessUnitScopes { get; set; } = [];
    public List<TerritoryResourceAssignmentViewModel> Items { get; set; } = [];
}

public sealed class TerritoryResourceAssignmentViewModel
{
    public Guid Id { get; set; }
    public Guid ModelId { get; set; }
    public Guid? TerritoryId { get; set; }
    public string? TerritoryCode { get; set; }
    public string? TerritoryName { get; set; }
    public string? TerritoryLevel { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceDisplayName { get; set; } = string.Empty;
    public string? ResourceEmail { get; set; }
    public Guid? PositionId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;
    public string PositionSourceSystem { get; set; } = string.Empty;
    public string CoverageScope { get; set; } = string.Empty;
    public List<string> BusinessUnitScopes { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public string AssignmentSource { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public bool IsExpired { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsPlanningOnly { get; set; }
    public string? ChangeReason { get; set; }
    public Guid? ReplacedAssignmentId { get; set; }
    public Guid? ReplacementAssignmentId { get; set; }
    public string? ReplacementReason { get; set; }
    public Guid? TransferFromAssignmentId { get; set; }
    public Guid? TransferToAssignmentId { get; set; }
    public string? TransferReason { get; set; }
}

// ---------------------------------------------------------------------------------------------------------------
// MOD-0151 FU04B — plan baseline vs current responsibility (READ-ONLY projection; no edit surface).
// ---------------------------------------------------------------------------------------------------------------

public sealed class TerritoryPlanVsCurrentViewModel
{
    public Guid ModelId { get; set; }
    public string ModelCode { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelStatus { get; set; } = string.Empty;

    /// <summary>"not-yet-activated" | "not-captured" | "available".</summary>
    public string State { get; set; } = string.Empty;

    public bool IsHistorical { get; set; }
    public Guid? PlanSnapshotId { get; set; }
    public int? SnapshotVersion { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public string? CapturedBy { get; set; }
    public string? ActivationCorrelationId { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public TerritoryPlanVsCurrentSummaryViewModel Summary { get; set; } = new();
    public List<TerritoryPlanVsCurrentRowViewModel> Rows { get; set; } = [];
}

public sealed class TerritoryPlanVsCurrentSummaryViewModel
{
    public int PlannedCount { get; set; }
    public int CurrentCount { get; set; }
    public int RowCount { get; set; }
    public int ChangedCount { get; set; }
    public Dictionary<string, int> CountsByDiffType { get; set; } = [];
}

public sealed class TerritoryPlanVsCurrentRowViewModel
{
    public string DiffType { get; set; } = string.Empty;
    public Guid ModelId { get; set; }
    public string ModelCode { get; set; } = string.Empty;
    public Guid? TerritoryNodeId { get; set; }
    public string TerritoryNodeCode { get; set; } = string.Empty;
    public string TerritoryNodeName { get; set; } = string.Empty;
    public List<string> BusinessUnitScopes { get; set; } = [];
    public string PositionCode { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;

    public string? PlannedResourceId { get; set; }
    public string? PlannedResourceDisplayName { get; set; }
    public DateTimeOffset? PlannedEffectiveFrom { get; set; }
    public DateTimeOffset? PlannedEffectiveTo { get; set; }
    public bool? PlannedIsPrimary { get; set; }
    public Guid? PlannedAssignmentId { get; set; }

    public string? CurrentResourceId { get; set; }
    public string? CurrentResourceDisplayName { get; set; }
    public string? CurrentPositionCode { get; set; }
    public string? CurrentPositionTitle { get; set; }
    public List<string> CurrentBusinessUnitScopes { get; set; } = [];
    public DateTimeOffset? CurrentEffectiveFrom { get; set; }
    public DateTimeOffset? CurrentEffectiveTo { get; set; }
    public bool? CurrentIsPrimary { get; set; }
    public Guid? CurrentAssignmentId { get; set; }
    public Guid? CurrentTerritoryNodeId { get; set; }
    public string? CurrentTerritoryNodeCode { get; set; }
    public string? CurrentStatus { get; set; }

    public string? ChangeReason { get; set; }
    public string? ReplacementReason { get; set; }
    public string? TransferReason { get; set; }
    public Guid? ReplacedAssignmentId { get; set; }
    public Guid? ReplacementAssignmentId { get; set; }
    public Guid? TransferFromAssignmentId { get; set; }
    public Guid? TransferToAssignmentId { get; set; }
    public DateTimeOffset? ChangedAt { get; set; }
    public string? ChangedBy { get; set; }
    public string? CorrelationId { get; set; }

    public List<string> SecondaryDifferences { get; set; } = [];

    /// <summary>Display-only legacy value; never a match key (pack §22.4).</summary>
    public string? LegacyRoleCode { get; set; }
}

public sealed class TerritoryResourceConflictReportViewModel
{
    public Guid ModelId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public int AssignmentCount { get; set; }
    public int ConflictCount { get; set; }
    public int WarningCount { get; set; }
    public List<TerritoryResourceConflictViewModel> Conflicts { get; set; } = [];
    public List<TerritoryResourceConflictViewModel> Warnings { get; set; } = [];
}

public sealed class TerritoryResourceConflictViewModel
{
    public string Kind { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<Guid> AssignmentIds { get; set; } = [];
    public string? PositionCode { get; set; }
    public Guid? TerritoryId { get; set; }
    public string? TerritoryCode { get; set; }
    public List<string> BusinessUnitScopes { get; set; } = [];
}

/// <summary>Resource assignment create/edit form model. The resource is captured as a PersonRef seam (id + display
/// snapshot) because MOD-0151 does not own an employee master (pack §10).</summary>
public sealed class TerritoryResourceAssignmentEditViewModel
{
    public Guid? Id { get; set; }

    public Guid? TerritoryId { get; set; }

    [Required]
    [StringLength(128)]
    public string ResourceId { get; set; } = string.Empty;

    [StringLength(32)]
    public string? ResourceType { get; set; }

    [Required]
    [StringLength(256)]
    public string ResourceDisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string? ResourceEmail { get; set; }

    public Guid? PositionId { get; set; }

    [Required]
    public string PositionCode { get; set; } = string.Empty;

    public string? PositionName { get; set; }
    public string? PositionType { get; set; }
    public string? PositionSourceSystem { get; set; }

    public string? CoverageScope { get; set; }

    public List<string>? BusinessUnitScopeCodes { get; set; }

    public bool IsPrimary { get; set; } = true;

    public string? AssignmentSource { get; set; }

    [DataType(DataType.Date)]
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    public DateTime? ValidTo { get; set; }

    [StringLength(512)]
    public string? ChangeReason { get; set; }
}

public sealed class TerritoryResourceAssignmentSavePayload
{
    public Guid? TerritoryId { get; set; }
    public TerritoryResourceRefPayload Resource { get; set; } = new();
    public Guid? PositionId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string? PositionName { get; set; }
    public string? PositionType { get; set; }
    public string? PositionSourceSystem { get; set; }
    public string? CoverageScope { get; set; }
    public List<string> BusinessUnitScopeCodes { get; set; } = [];
    public bool IsPrimary { get; set; }
    public string? AssignmentSource { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? ChangeReason { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class TerritoryResourceRefPayload
{
    public string ResourceId { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>Shape used to read whatever person/employee list the platform exposes. Deliberately tolerant: FU04 only
/// needs an id and something to display, and treats an unavailable source as "lookup not ready".</summary>
public sealed class ResourceLookupPage
{
    public List<ResourceLookupItem> Items { get; set; } = [];
}

public sealed class ResourceLookupItem
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
}

// ============================================================================================================
// MOD-0151 FU08 — Import / Export view models. Read-only shapes over the Gateway payloads; the browser never
// posts a TenantId and never talks to CrmService directly.
// ============================================================================================================

public sealed class TerritoryImportPreviewViewModel
{
    public string CorrelationId { get; set; } = string.Empty;
    public Guid ModelId { get; set; }
    public string ModelCode { get; set; } = string.Empty;
    public string ModelStatus { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public bool Applied { get; set; }
    public bool CanApply { get; set; }
    public string? BlockedReason { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public bool StrictMode { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public int PreviousAppliesOfThisFile { get; set; }
    public Guid? ImportRunId { get; set; }
    public string? RunStatus { get; set; }
    public TerritoryImportSummaryViewModel Summary { get; set; } = new();
    public List<string> FileErrors { get; set; } = [];
    public List<string> FileWarnings { get; set; } = [];
    public List<TerritoryImportSheetOutcomeViewModel> Sheets { get; set; } = [];
    public List<TerritoryImportRowViewModel> Rows { get; set; } = [];
}

public sealed class TerritoryImportSummaryViewModel
{
    public int TotalRows { get; set; }
    public int Creates { get; set; }
    public int Updates { get; set; }
    public int Ends { get; set; }
    public int Skips { get; set; }
    public int Errors { get; set; }
    public int Conflicts { get; set; }
    public int Warnings { get; set; }
}

public sealed class TerritoryImportSheetOutcomeViewModel
{
    public string Sheet { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int BlockingRows { get; set; }
    public bool Applied { get; set; }
    public string? NotAppliedReason { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Ended { get; set; }
    public int Skipped { get; set; }
}

public sealed class TerritoryImportRowViewModel
{
    public string Sheet { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
    public bool Blocking { get; set; }
    public string? Operation { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? ResolvedKey { get; set; }
    public List<string> ChangedFields { get; set; } = [];
    public string Status { get; set; } = string.Empty;
}

public sealed class TerritoryImportRunListViewModel
{
    public int TotalCount { get; set; }
    public List<TerritoryImportRunViewModel> Items { get; set; } = [];
}

public sealed class TerritoryImportRunViewModel
{
    public Guid ImportRunId { get; set; }
    public string ModelCode { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? AppliedAt { get; set; }
    public string? AppliedBy { get; set; }
    public string? CorrelationId { get; set; }
    public int TotalRows { get; set; }
    public int Creates { get; set; }
    public int Updates { get; set; }
    public int Ends { get; set; }
    public int Skips { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public bool StrictMode { get; set; }
    public List<string> SheetOutcomes { get; set; } = [];
}
