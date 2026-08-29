namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0150 FU04 — directional Account↔Account relationship (Hospital↔Pharmacy, refers-to, served-by, same-network…).
/// Stored directional (Source→Target). <see cref="Direction"/> is DERIVED from the account-relationship-type metadata
/// (bidirectional vs directional), never taken blindly from the request. Inverse display uses the type's
/// <c>inverseLabelCode</c> metadata. RelationshipType/Status are MOD-0048 published values. Self-link forbidden unless
/// the type's <c>selfAllowed</c> metadata is true. Uniqueness: one active per (Tenant, Source, Target, Type); for
/// bidirectional types the reverse (Target→Source, Type) is treated as the same link.
/// </summary>
public sealed class AccountRelationship : EntityBase
{
    public Guid SourceAccountId { get; set; }
    public Guid TargetAccountId { get; set; }

    /// <summary>MOD-0048 published account-relationship-type value code (required).</summary>
    public string RelationshipType { get; set; } = string.Empty;

    /// <summary>Derived from type metadata: "bidirectional" or "outbound".</summary>
    public string Direction { get; set; } = "outbound";

    /// <summary>MOD-0048 published account-relationship-status value code (required).</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }

    /// <summary>MOD-0150 hardening: business justification recorded when the two accounts are in different countries
    /// (controlled cross-country relationship). Non-empty only for cross-country links; never written raw to audit/log.</summary>
    public string? CrossCountryReason { get; set; }
}
