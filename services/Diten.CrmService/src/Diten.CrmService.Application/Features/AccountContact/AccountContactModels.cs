namespace Diten.CrmService.Application.Features.AccountContact;

public sealed record AccountContactLinkDto(
    Guid Id,
    Guid AccountId,
    Guid ContactId,
    string RoleCode,
    bool IsPrimary,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ReportsToContactId = null);

/// <summary>Account 360 "Related Contacts" projection row (join AccountContactLink + Contact). ReportsTo = in-account manager.</summary>
public sealed record AccountRelatedContactDto(
    Guid LinkId,
    Guid AccountId,
    Guid ContactId,
    string DisplayName,
    string ContactType,
    string RoleCode,
    bool IsPrimary,
    string Status,
    string? Phone,
    string? Email,
    Guid? ReportsToContactId = null,
    string? ReportsToName = null,
    string? Specialty = null,
    string? PhotoDataUri = null);

/// <summary>Contact 360 "Linked Accounts" projection row (join AccountContactLink + Account).</summary>
public sealed record ContactLinkedAccountDto(
    Guid LinkId,
    Guid ContactId,
    Guid AccountId,
    string AccountName,
    string AccountCode,
    string AccountType,
    string RoleCode,
    bool IsPrimary,
    string Status);
