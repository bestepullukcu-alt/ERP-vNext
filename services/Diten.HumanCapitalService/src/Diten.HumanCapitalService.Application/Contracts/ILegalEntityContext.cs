namespace Diten.HumanCapitalService.Application.Contracts;

/// <summary>
/// Legal-entity scoping context for the request, sitting alongside <see cref="ITenantContext"/>.
/// The selection comes from the <c>X-Legal-Entity-Id</c> header (write target); the actable set
/// is derived from the JWT <c>legal_entities</c> claim expanded with descendants resolved from MDM.
/// Tenant scoping is unaffected — legal-entity scoping is an additional narrowing within a tenant.
/// </summary>
public interface ILegalEntityContext
{
    /// <summary>Selected legal entity (write target) from the <c>X-Legal-Entity-Id</c> header; null when unset.</summary>
    Guid? SelectedLegalEntityId { get; }

    /// <summary>
    /// True only when a legal entity is selected AND it is within the caller's actable set
    /// (assigned legal entities plus their descendants). Writes must fail closed (403) otherwise.
    /// </summary>
    Task<bool> IsSelectionAllowedAsync(CancellationToken ct);

    /// <summary>
    /// Read roll-up scope. When a legal entity is selected, this is {selected} ∪ descendants(selected)
    /// intersected with the actable set; otherwise it is the caller's full actable set. An empty result
    /// means the caller can see nothing under legal-entity scoping.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetEffectiveLegalEntityIdsAsync(CancellationToken ct);
}
