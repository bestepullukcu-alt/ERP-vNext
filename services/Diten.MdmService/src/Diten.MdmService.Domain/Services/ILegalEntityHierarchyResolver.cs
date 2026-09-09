namespace Diten.MdmService.Domain.Services;

/// <summary>
/// Resolves the legal-entity hierarchy (roll-up scope) for the current tenant.
/// Consumers (auth assignment, operational roll-up filters) use the returned id set to scope data
/// to a legal entity and all of its descendants.
/// </summary>
public interface ILegalEntityHierarchyResolver
{
    /// <summary>
    /// Returns { root + every descendant } legal-entity id for the given root, within the current tenant.
    /// Returns null when the root does not exist (or is not visible) in the tenant. Cycle-safe.
    /// </summary>
    Task<IReadOnlyList<Guid>?> GetSelfAndDescendantIdsAsync(Guid rootLegalEntityId, CancellationToken cancellationToken = default);
}
