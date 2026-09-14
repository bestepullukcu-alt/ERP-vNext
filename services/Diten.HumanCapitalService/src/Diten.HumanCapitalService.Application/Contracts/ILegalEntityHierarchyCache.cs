namespace Diten.HumanCapitalService.Application.Contracts;

/// <summary>
/// Resolves the legal-entity hierarchy owned by MDM (id/parentId), cached with a short TTL so
/// scoping does not issue a downstream call per request. Fail-safe: when the hierarchy cannot be
/// resolved, callers receive only the requested id (self), never a broader set.
/// </summary>
public interface ILegalEntityHierarchyCache
{
    /// <summary>Returns {legalEntityId} ∪ all transitive descendants of it.</summary>
    Task<IReadOnlyCollection<Guid>> GetSelfAndDescendantsAsync(Guid legalEntityId, CancellationToken ct);
}
