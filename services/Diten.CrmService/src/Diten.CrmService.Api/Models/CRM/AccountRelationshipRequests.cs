namespace Diten.CrmService.Api.Models.CRM;

/// <summary>Body for POST /api/crm/accounts/{accountId}/relationships (accountId is the source, from the route).</summary>
public sealed record CreateAccountRelationshipRequest(
    Guid TargetAccountId,
    string RelationshipType,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    // MOD-0150 hardening: business reason, required only for a cross-country Account↔Account relationship.
    string? CrossCountryReason = null);

/// <summary>Body for PUT /api/crm/accounts/{accountId}/relationships/{relationshipId}.</summary>
public sealed record UpdateAccountRelationshipRequest(
    string RelationshipType,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    string? CrossCountryReason = null);
