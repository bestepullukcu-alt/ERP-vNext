using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0149 Customer 360 / Account Hierarchy — Golden Reference Compact view models.
/// NOTE: Account owns location foundation only. Coverage (Territory/Zone/MicroZone) is owned by MOD-0151 and is
/// exposed read-only via <see cref="CoverageSummaryViewModel"/>; there is deliberately NO ZoneId/MicroZoneId/
/// TerritoryId/SalesRepId field here (see module pack §3.1).
/// </summary>
public sealed class AccountEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Optional on input — the backend generates ACC-{YYYY}-{sequence} when left blank.</summary>
    [StringLength(64)]
    [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "AccountCodeInvalidChars")]
    public string? AccountCode { get; set; }

    [Required]
    public string AccountType { get; set; } = string.Empty;

    public string? AccountCategory { get; set; }

    public Guid? ParentAccountId { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }

    [StringLength(500)]
    public string? AddressLine { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [StringLength(200)]
    public string? ResponsiblePersonName { get; set; }

    [StringLength(32)]
    public string? ResponsiblePersonPhone { get; set; }

    [StringLength(256)]
    [EmailAddress]
    public string? ResponsiblePersonEmail { get; set; }

    /// <summary>Quick-entry external/legacy id. Persisted by the backend as an AccountExternalReference
    /// (SourceSystem + ExternalId); never a replacement for AccountCode.</summary>
    [StringLength(128)]
    public string? ExternalReference { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Optional account logo carried as a base64 image data URI (<c>data:image/...;base64,...</c>).
    /// Populated client-side from the file picker; capped at ~256&#160;KB before it is posted. Empty when no logo.</summary>
    [StringLength(700_000)]
    public string? LogoDataUri { get; set; }

    // Reference options (MOD-0048 published-values). Never hardcoded.
    public IReadOnlyList<ReferenceOptionViewModel> AccountTypeOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> AccountStatusOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> AccountCategoryOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> ParentAccountOptions { get; set; } = [];

    // Location reference options — independent (non-cascading) single-selects sourced from MOD-0048.
    // The backend stores CountryRef/CityRef/DistrictRef as free strings (no server-side validation), so these
    // dropdowns exist purely to standardize input; they stay empty until the sets are published.
    public IReadOnlyList<ReferenceOptionViewModel> CountryOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> CityOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> DistrictOptions { get; set; } = [];

    /// <summary>Set when a required MOD-0048 reference set could not be read (controlled dependency state).
    /// The form must show this and must NOT fall back to a local list.</summary>
    public string? ReferenceDependencyMessage { get; set; }
}

public sealed record ReferenceOptionViewModel(string Value, string Text);

public sealed class AccountExternalReferenceViewModel
{
    public Guid Id { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? SourceEntity { get; set; }
    public string? DisplayName { get; set; }
    public string? Notes { get; set; }
}

public sealed class AccountAttributeViewModel
{
    public string AttributeCode { get; set; } = string.Empty;
    public string? Value { get; set; }
}

/// <summary>Read-only coverage projection. MOD-0149 does not own or persist territory/zone (MOD-0151 owns it).</summary>
public sealed class CoverageSummaryViewModel
{
    public string? Status { get; set; }
    public string? Source { get; set; }
}

/// <summary>Read-only current MOD-0151 territory coverage row for Account Details — mirrors one
/// <c>AccountTerritoryAssignmentDto</c> returned by GET /api/crm/accounts/{id}/territory-coverage-summary.
/// MOD-0149 never persists territory on the Account master.</summary>
public sealed class TerritoryCoverageAssignmentViewModel
{
    public Guid Id { get; set; }
    public Guid TerritoryNodeId { get; set; }
    public string TerritoryNodeCode { get; set; } = string.Empty;
    public string TerritoryNodeName { get; set; } = string.Empty;
    public string? AssignmentSource { get; set; }
    public string? AssignmentStatus { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

/// <summary>Shape of GET /api/crm/accounts/{id}/territory-assignments (Response&lt;AccountTerritoryAssignmentListDto&gt;) —
/// the full assignment history (current + ended), newest first.</summary>
public sealed class TerritoryAssignmentListModel
{
    public int TotalCount { get; set; }
    public List<TerritoryCoverageAssignmentViewModel> Items { get; set; } = [];
}

public sealed class AccountListItemViewModel
{
    public Guid Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountCategory { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ParentAccountId { get; set; }
    public string? LogoDataUri { get; set; }
    public string? CountryRef { get; set; }

    // Current (effective-now) MOD-0151 territory coverage projected onto the row (read-only). Null when unassigned;
    // TerritoryNodeName carries every current node joined when the account is covered by more than one node.
    public Guid? TerritoryNodeId { get; set; }
    public string? TerritoryNodeCode { get; set; }
    public string? TerritoryNodeName { get; set; }
    public string? TerritoryCountryScope { get; set; }
}

public sealed class AccountDetailViewModel
{
    public Guid Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountCategory { get; set; }
    public Guid? ParentAccountId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }
    public string? AddressLine { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public string? ResponsiblePersonPhone { get; set; }
    public string? ResponsiblePersonEmail { get; set; }
    public string? Notes { get; set; }
    public string? LogoDataUri { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<AccountExternalReferenceViewModel> ExternalReferences { get; set; } = [];
    public List<AccountAttributeViewModel> Attributes { get; set; } = [];
}

/// <summary>Shape of GET /api/crm/accounts/{id}/overview (Account 360).
/// The related-contacts / related-accounts projections below are NOT part of the /overview payload — they are
/// populated frontend-side by <c>AccountsController.Details</c> from the MOD-0150 FU03/FU04 projection endpoints
/// (permission-gated), and stay empty when unauthorized or unavailable. Read-only; no Account-master pollution.</summary>
public sealed class AccountOverviewViewModel
{
    public AccountDetailViewModel? Account { get; set; }
    public Guid? ParentAccountId { get; set; }
    public List<AccountListItemViewModel> Children { get; set; } = [];
    public CoverageSummaryViewModel? Coverage { get; set; }

    // ---- Territory coverage (MOD-0151 AccountTerritoryAssignment projection) — frontend-populated ----
    /// <summary>True when the caller carries <c>crm.territory.model.read</c>; false → section shows a not-authorized message.</summary>
    public bool TerritoryCoverageAuthorized { get; set; }
    /// <summary>True when the coverage endpoint could not be reached / returned a non-success (controlled dependency).</summary>
    public bool TerritoryCoverageUnavailable { get; set; }
    /// <summary>Full territory assignment history (current + ended) for this account, newest first; empty when
    /// unassigned/unauthorized/unavailable. Rendered as a read-only DataTable on Details.</summary>
    public List<TerritoryCoverageAssignmentViewModel> TerritoryAssignments { get; set; } = [];

    // ---- Related Contacts (MOD-0150 FU03 AccountContactLink projection) — frontend-populated ----
    /// <summary>True when the caller carries <c>crm.account-contact.read</c>; false → section shows a not-authorized message.</summary>
    public bool RelatedContactsAuthorized { get; set; }
    /// <summary>True when the caller carries <c>crm.account-contact.manage</c> → Add/Edit/End actions are shown.</summary>
    public bool RelatedContactsCanManage { get; set; }
    /// <summary>True when the projection endpoint could not be reached / returned a non-success (controlled dependency).</summary>
    public bool RelatedContactsUnavailable { get; set; }
    public List<AccountRelatedContactViewModel> RelatedContacts { get; set; } = [];
    /// <summary>Golden Slim create/edit canvas model for the Related Contacts section — carries the live contact/role
    /// options and the dependency message. Populated only when the caller carries <c>crm.account-contact.manage</c>.</summary>
    public AccountContactLinkEditViewModel? ContactLinkForm { get; set; }

    // ---- Related Accounts (MOD-0150 FU04 AccountRelationship projection) — frontend-populated ----
    /// <summary>True when the caller carries <c>crm.account-relationship.read</c>; false → section shows a not-authorized message.</summary>
    public bool RelatedAccountsAuthorized { get; set; }
    /// <summary>True when the caller carries <c>crm.account-relationship.manage</c> → Add/Edit/End actions are shown.</summary>
    public bool RelatedAccountsCanManage { get; set; }
    /// <summary>True when the projection endpoint could not be reached / returned a non-success (controlled dependency).</summary>
    public bool RelatedAccountsUnavailable { get; set; }
    public List<AccountRelatedAccountViewModel> RelatedAccounts { get; set; } = [];
    /// <summary>Golden Slim create/edit offcanvas model for the Related Accounts section — carries the live MOD-0048
    /// reference options and the dependency message. Populated only when the caller carries
    /// <c>crm.account-relationship.manage</c>; null → the section renders read-only.</summary>
    public AccountRelationshipEditViewModel? RelationshipForm { get; set; }
}

/// <summary>Read-only Account 360 "Related Contacts" row — mirrors CrmService <c>AccountRelatedContactDto</c>
/// (GET /api/crm/accounts/{id}/related-contacts). Frontend read model only; no Contact fields on the Account master.</summary>
public sealed class AccountRelatedContactViewModel
{
    public Guid LinkId { get; set; }
    public Guid ContactId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ContactType { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>MOD-0150 in-account hierarchy: manager contact (within this account) + resolved name.</summary>
    public Guid? ReportsToContactId { get; set; }
    public string? ReportsToName { get; set; }
}

/// <summary>Read-only Account 360 "Related Accounts" row — mirrors CrmService <c>RelatedAccountDto</c>
/// (GET /api/crm/accounts/{id}/related-accounts). <see cref="EffectiveLabelCode"/> is already resolved from THIS
/// account's perspective (direct type or inverse label). Frontend read model only; no relationship array on the master.</summary>
public sealed class AccountRelatedAccountViewModel
{
    public Guid AccountRelationshipId { get; set; }
    /// <summary>Stored source of the relationship. Edit/End only work when THIS account is the source (backend guard),
    /// so management actions are shown only for source-owned rows; inverse rows are managed from the source account.</summary>
    public Guid SourceAccountId { get; set; }
    public Guid RelatedAccountId { get; set; }
    public string RelatedAccountName { get; set; } = string.Empty;
    public string RelatedAccountCode { get; set; } = string.Empty;
    public string RelatedAccountType { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string? InverseLabelCode { get; set; }
    public string DisplayDirection { get; set; } = string.Empty;
    public string EffectiveLabelCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Body posted to the CRM backend. Mirrors CreateAccountCommand / UpdateAccountCommand.
/// TenantId is never sent — it is resolved server-side.</summary>
public sealed class AccountSavePayload
{
    public string AccountName { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string? AccountCategory { get; set; }
    public Guid? ParentAccountId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }
    public string? AddressLine { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public string? ResponsiblePersonPhone { get; set; }
    public string? ResponsiblePersonEmail { get; set; }
    public string? Notes { get; set; }
    public string? LogoDataUri { get; set; }
    public ExternalReferenceInputPayload? ExternalReference { get; set; }
}

public sealed class ExternalReferenceInputPayload
{
    public string ExternalId { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }
    public string? SourceEntity { get; set; }
    public string? DisplayName { get; set; }
}

public sealed class AccountPagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>Gateway response envelope mirror (declared per module, matching the Golden Reference vertical).</summary>
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>MOD-0048 published-values consumer shape: Response&lt;BusinessReferenceDataPublishedValuesModel&gt;.</summary>
public sealed class PublishedValuesModel
{
    public string? SetCode { get; set; }
    public int VersionNumber { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<PublishedValueItemModel> Items { get; set; } = [];
}

/// <summary>
/// A single MOD-0048 published value. The consumer endpoint emits <c>code</c>/<c>label</c>; older/alternate
/// shapes used <c>valueCode</c>/<c>displayName</c>, so both are bound and resolved via <see cref="Value"/>/<see cref="Text"/>.
/// </summary>
public sealed class PublishedValueItemModel
{
    public string? Code { get; set; }
    public string? ValueCode { get; set; }
    public string? Label { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public string? Value => !string.IsNullOrWhiteSpace(Code) ? Code : ValueCode;
    public string? Text => !string.IsNullOrWhiteSpace(Label) ? Label : DisplayName;
}
