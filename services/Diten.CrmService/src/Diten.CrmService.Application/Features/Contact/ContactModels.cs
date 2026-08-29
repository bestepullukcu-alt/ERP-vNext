namespace Diten.CrmService.Application.Features.Contact;

public sealed record ContactListItemDto(
    Guid Id,
    string DisplayName,
    string FirstName,
    string LastName,
    string ContactType,
    string Status,
    string? ProfessionalTitle,
    string? Email,
    string? Phone,
    // Small avatar for the list thumbnail (already loaded with the entity; authorized-UI only, still off export/audit).
    string? PhotoDataUri = null,
    // Current territory coverage derived from the contact's linked accounts (MOD-0151). A contact can be linked to
    // several accounts, so these are distinct sets across all of them — read-only, never persisted on the Contact.
    IReadOnlyList<string>? TerritoryCountryScopes = null,
    IReadOnlyList<string>? TerritoryNodeNames = null);

public sealed record ContactExternalReferenceDto(
    Guid Id,
    string SourceSystem,
    string ExternalId,
    string? SourceEntity,
    string? DisplayName,
    string? Notes);

public sealed record ContactDetailDto(
    Guid Id,
    string DisplayName,
    string FirstName,
    string LastName,
    string ContactType,
    string Status,
    string? ProfessionalTitle,
    string? Specialty,
    string? Department,
    string? Phone,
    string? Email,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ContactExternalReferenceDto> ExternalReferences,
    // MOD-0150 Contact Location Hardening — optional location context.
    string? CountryRef = null,
    string? CityRef = null,
    string? DistrictRef = null,
    string? AddressLine = null,
    string? PostalCode = null,
    string? PreferredLanguage = null,
    string? PhoneCountryCode = null,
    string? Gender = null,
    string? PhotoDataUri = null)
{
    /// <summary>True when the Contact has no country code — the UI surfaces a "Location incomplete" hint (non-blocking).</summary>
    public bool IsLocationIncomplete => string.IsNullOrWhiteSpace(CountryRef);
}

/// <summary>
/// Contact 360 read model. FU01 exposes the contact profile + its external references; FU03 adds the linked-accounts
/// projection (AccountContactLink); FU05 adds the read-only consent/preference seam summary (MOD-0164, no-op until it
/// ships). None of these projections are ever faked.
/// </summary>
public sealed record ContactOverviewDto(
    ContactDetailDto Contact,
    IReadOnlyList<ContactAccountLinkSummaryDto> LinkedAccounts,
    ConsentPreference.ContactConsentPreferenceSummaryDto ConsentPreferenceSummary,
    IReadOnlyList<ContactTerritoryCoverageDto> TerritoryCoverage);

/// <summary>One current territory-coverage row on Contact 360, contributed by a linked account (MOD-0151). The
/// contact holds no territory of its own — this is projected from each linked account's current assignment.</summary>
public sealed record ContactTerritoryCoverageDto(
    Guid AccountId,
    string AccountName,
    string AccountCode,
    Guid TerritoryNodeId,
    string TerritoryNodeCode,
    string TerritoryNodeName,
    string? CountryScope,
    string AssignmentStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

/// <summary>Summary of an account a contact is linked to (FU03 AccountContactLink join). Empty until links exist.</summary>
public sealed record ContactAccountLinkSummaryDto(
    Guid LinkId,
    Guid AccountId,
    string AccountName,
    string AccountCode,
    string AccountType,
    string RoleCode,
    bool IsPrimary,
    string Status);

public sealed record ContactSearchResultDto(Guid Id, string DisplayName, string ContactType, string Status);

/// <summary>External-reference quick-entry carried on create. Persisted into ContactExternalReference with the
/// supplied or default SourceSystem; never kept as a Contact master string field.</summary>
public sealed record ContactExternalReferenceInput(string ExternalId, string? SourceSystem, string? SourceEntity, string? DisplayName);
