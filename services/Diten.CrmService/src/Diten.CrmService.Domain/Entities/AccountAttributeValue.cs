namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// Account-level controlled attribute value surface (§10.2). NOT a generic custom-field engine.
/// Attribute-definition SoR is EA-TBD; this only stores account-scoped values keyed by a stable AttributeCode.
/// Unique per (TenantId, AccountId, AttributeCode).
/// </summary>
public sealed class AccountAttributeValue : EntityBase
{
    public Guid AccountId { get; set; }

    /// <summary>Stable attribute key (kebab-case).</summary>
    public string AttributeCode { get; set; } = string.Empty;

    public string? Value { get; set; }
}
