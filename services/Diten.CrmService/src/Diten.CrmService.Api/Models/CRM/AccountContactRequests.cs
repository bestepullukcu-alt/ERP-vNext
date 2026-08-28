namespace Diten.CrmService.Api.Models.CRM;

/// <summary>Body for POST /api/crm/accounts/{accountId}/contacts (AccountId comes from the route).</summary>
public sealed record LinkContactToAccountRequest(
    Guid ContactId,
    string RoleCode,
    bool IsPrimary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    // MOD-0150 hardening: business reason, required only for a cross-country Contact↔Account link.
    string? CrossCountryReason = null,
    // MOD-0150 in-account hierarchy: manager contact within this account (optional).
    Guid? ReportsToContactId = null);

/// <summary>Body for PUT /api/crm/accounts/{accountId}/contacts/{linkId}. <see cref="Status"/> is the historical
/// lifecycle marker (e.g. "ended"/"inactive" for an End action); null keeps the link's current status.</summary>
public sealed record UpdateAccountContactLinkRequest(
    string RoleCode,
    bool IsPrimary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    string? Status = null,
    string? CrossCountryReason = null,
    Guid? ReportsToContactId = null);
