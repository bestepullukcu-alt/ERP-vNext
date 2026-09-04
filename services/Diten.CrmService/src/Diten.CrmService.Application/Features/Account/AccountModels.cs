namespace Diten.CrmService.Application.Features.Account;

// UnfilteredTotal is the tenant-wide count ignoring the search term (DataTables recordsTotal); Total is the filtered
// count (recordsFiltered). UnfilteredTotal defaults to 0 for callers that do not populate it (server-side grids only).
public sealed record PagedResult<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize, long UnfilteredTotal = 0);

public sealed record AccountListItemDto(
    Guid Id,
    string AccountName,
    string AccountCode,
    string AccountType,
    string? AccountCategory,
    string Status,
    Guid? ParentAccountId,
    string? LogoDataUri = null,
    string? CountryRef = null,
    // Current (effective-now) territory coverage projected from MOD-0151 AccountTerritoryAssignment. Read-only;
    // MOD-0149 never persists territory on the Account master. Null when the account has no current assignment.
    // TerritoryNodeName carries every current node joined when an account is covered by more than one node.
    // TerritoryCountryScope is the primary current node's CountryCode (the grid's country-scope column/filter).
    Guid? TerritoryNodeId = null,
    string? TerritoryNodeCode = null,
    string? TerritoryNodeName = null,
    string? TerritoryCountryScope = null);

public sealed record AccountExternalReferenceDto(
    Guid Id,
    string SourceSystem,
    string ExternalId,
    string? SourceEntity,
    string? DisplayName,
    string? Notes);

public sealed record AccountAttributeDto(string AttributeCode, string? Value);

/// <summary>Read-only coverage projection placeholder. MOD-0149 does NOT own/persist territory/zone.
/// When MOD-0151 is available it supplies Territory/Zone/MicroZone as a read-only projection (§3.1).</summary>
public sealed record CoverageSummaryDto(string Status, string Source)
{
    public static CoverageSummaryDto NotAvailable() => new("not-available", "MOD-0151");
}

public sealed record AccountDetailDto(
    Guid Id,
    string AccountName,
    string AccountCode,
    string AccountType,
    string? AccountCategory,
    Guid? ParentAccountId,
    string Status,
    string? CountryRef,
    string? CityRef,
    string? DistrictRef,
    string? AddressLine,
    double? Latitude,
    double? Longitude,
    string? ResponsiblePersonName,
    string? ResponsiblePersonPhone,
    string? ResponsiblePersonEmail,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<AccountExternalReferenceDto> ExternalReferences,
    IReadOnlyList<AccountAttributeDto> Attributes,
    string? LogoDataUri);

public sealed record AccountOverviewDto(
    AccountDetailDto Account,
    Guid? ParentAccountId,
    IReadOnlyList<AccountListItemDto> Children,
    CoverageSummaryDto Coverage);

public sealed record AccountHierarchyNodeDto(
    Guid Id,
    string AccountName,
    string AccountCode,
    IReadOnlyList<AccountHierarchyNodeDto> Children);

/// <summary>External-reference quick-entry carried on create (§10.1b). Persisted into AccountExternalReference
/// with the supplied or default SourceSystem; never kept as an Account master string field.</summary>
public sealed record ExternalReferenceInput(string ExternalId, string? SourceSystem, string? SourceEntity, string? DisplayName);
