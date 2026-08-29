using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// NOTE: ReferenceOptionViewModel and GatewayResponse<T> are declared once in AccountViewModels.cs (same
// Diten.Web.Models.CRM namespace) and reused here — do not redefine them.

/// <summary>
/// MOD-0150 FU02 Contact & Relationship Management — Golden Reference Compact vertical (Contact foundation).
/// NO AccountContactLink / AccountRelationship / Zone-Territory / Consent-capture fields — those are later FUs / other modules.
/// </summary>
public sealed class ContactEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(120)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(120)]
    public string? LastName { get; set; }

    /// <summary>Optional on input — the backend derives "FirstName LastName" when left blank.</summary>
    [StringLength(200)]
    public string? DisplayName { get; set; }

    [Required]
    public string ContactType { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>MOD-0048 gender value code (optional). Personal data.</summary>
    [StringLength(32)]
    public string? Gender { get; set; }

    /// <summary>Optional avatar as a base64 image data-URI (client-resized). Personal data — excluded from list/export/audit.</summary>
    [StringLength(700_000)]
    public string? PhotoDataUri { get; set; }

    [StringLength(120)]
    public string? ProfessionalTitle { get; set; }

    [StringLength(120)]
    public string? Specialty { get; set; }

    [StringLength(120)]
    public string? Department { get; set; }

    [StringLength(32)]
    public string? Phone { get; set; }

    [StringLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>Quick-entry external/legacy id. Persisted as a ContactExternalReference (SourceSystem + ExternalId).</summary>
    [StringLength(128)]
    public string? ExternalReference { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    // MOD-0150 Contact Location Hardening — all optional; Country is never required (an incomplete location is allowed).
    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }

    [StringLength(256)]
    public string? AddressLine { get; set; }

    [StringLength(16)]
    public string? PostalCode { get; set; }

    [StringLength(16)]
    public string? PreferredLanguage { get; set; }

    [StringLength(16)]
    public string? PhoneCountryCode { get; set; }

    // Reference options (MOD-0048 published-values). Never hardcoded.
    public IReadOnlyList<ReferenceOptionViewModel> ContactTypeOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> ContactStatusOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> CountryOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> CityOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> DistrictOptions { get; set; } = [];
    // Professional reference sets (MOD-0048; optional, pack §10). select2, fallback-option for stored value.
    public IReadOnlyList<ReferenceOptionViewModel> ProfessionalTitleOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> SpecialtyOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> DepartmentOptions { get; set; } = [];
    // Phone dial code + preferred language (MOD-0048; optional). select2, fallback-option for stored value.
    public IReadOnlyList<ReferenceOptionViewModel> PhoneCountryCodeOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> PreferredLanguageOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> GenderOptions { get; set; } = [];

    /// <summary>Set when a required MOD-0048 reference set could not be read (controlled dependency state).</summary>
    public string? ReferenceDependencyMessage { get; set; }
}

public sealed class ContactExternalReferenceViewModel
{
    public Guid Id { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? SourceEntity { get; set; }
    public string? DisplayName { get; set; }
    public string? Notes { get; set; }
}

public sealed class ContactListItemViewModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProfessionalTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Current territory coverage derived from the contact's linked accounts (MOD-0151). Several linked accounts ⇒
    // several distinct nodes / country scopes; both drive the grid column and the (cascading) inline filters.
    public List<string> TerritoryCountryScopes { get; set; } = [];
    public List<string> TerritoryNodeNames { get; set; } = [];
}

public sealed class ContactDetailViewModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? PhotoDataUri { get; set; }
    public string? ProfessionalTitle { get; set; }
    public string? Specialty { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? PhoneCountryCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<ContactExternalReferenceViewModel> ExternalReferences { get; set; } = [];

    /// <summary>True when no country is set — Details/Edit surface a non-blocking "Location incomplete" hint.</summary>
    public bool IsLocationIncomplete => string.IsNullOrWhiteSpace(CountryRef);
}

/// <summary>Read-only summary of an account this contact is linked to (FU03 AccountContactLink join).</summary>
public sealed class ContactAccountLinkSummaryViewModel
{
    public Guid LinkId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? RoleCode { get; set; }
    public bool IsPrimary { get; set; }
    public string? Status { get; set; }
}

/// <summary>Shape of GET /api/crm/contacts/{id}/overview (Contact 360).</summary>
public sealed class ContactOverviewViewModel
{
    public ContactDetailViewModel? Contact { get; set; }
    public List<ContactAccountLinkSummaryViewModel> LinkedAccounts { get; set; } = [];
    public ContactConsentPreferenceSummaryViewModel? ConsentPreferenceSummary { get; set; }

    /// <summary>Current territory coverage of this contact, projected from its linked accounts (MOD-0151). One row per
    /// (linked account × its current territory node); empty when no linked account is currently covered.</summary>
    public List<ContactTerritoryCoverageViewModel> TerritoryCoverage { get; set; } = [];

    /// <summary>MOD-0150 FU07 availability, one panel per linked account/location. Read-only on the 360 page; the
    /// editor lives on the Availability page. Never a contact-level schedule.</summary>
    public List<LinkAvailabilityViewModel> Availability { get; set; } = [];
}

/// <summary>One Contact-360 territory row contributed by a linked account (MOD-0151). Read-only; the contact holds
/// no territory of its own.</summary>
public sealed class ContactTerritoryCoverageViewModel
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public Guid TerritoryNodeId { get; set; }
    public string TerritoryNodeCode { get; set; } = string.Empty;
    public string TerritoryNodeName { get; set; } = string.Empty;
    public string? CountryScope { get; set; }
    public string? AssignmentStatus { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
}

/// <summary>Read-only MOD-0164 consent/preference seam summary (FU05). No-op until MOD-0164 ships; never captured here.</summary>
public sealed class ContactConsentPreferenceSummaryViewModel
{
    public bool ConsentAvailable { get; set; }
    public bool PreferenceAvailable { get; set; }
    public string ConsentStatus { get; set; } = string.Empty;
    public string PreferenceStatus { get; set; } = string.Empty;
    public DateTimeOffset? LastConsentUpdatedAt { get; set; }
    public DateTimeOffset? LastPreferenceUpdatedAt { get; set; }
    public List<ContactConsentPreferenceChannelViewModel> Channels { get; set; } = [];
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public bool IsNotAuthorized => string.Equals(ConsentStatus, "not-authorized", StringComparison.OrdinalIgnoreCase);
    public bool HasData => ConsentAvailable || PreferenceAvailable;
}

/// <summary>Per-channel read-only consent/preference row (MOD-0164).</summary>
public sealed class ContactConsentPreferenceChannelViewModel
{
    public string ChannelCode { get; set; } = string.Empty;
    public string ConsentState { get; set; } = string.Empty;
    public string PreferenceState { get; set; } = string.Empty;
    public DateTimeOffset? LastUpdatedAt { get; set; }
}

/// <summary>Body posted to the CRM backend. Mirrors CreateContactCommand / UpdateContactCommand. TenantId is server-resolved.</summary>
public sealed class ContactSavePayload
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string ContactType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? PhotoDataUri { get; set; }
    public string? ProfessionalTitle { get; set; }
    public string? Specialty { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? PhoneCountryCode { get; set; }
    public ContactExternalReferenceInputPayload? ExternalReference { get; set; }
}

public sealed class ContactExternalReferenceInputPayload
{
    public string ExternalId { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }
    public string? SourceEntity { get; set; }
    public string? DisplayName { get; set; }
}
