using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0162 FU05 ContentEngagementJourney master — the ONLY repository of this FU (S2: stages are embedded, no second
/// collection). Tenant scoped and soft-delete aware. There is deliberately <b>no delete method</b>: closing a journey
/// (or a stage, in the same document) is the soft archive lifecycle, so journey/stage history stays readable. Every
/// write is a single-document replace guarded by the optimistic <see cref="EntityBase.Version"/> token, so no
/// multi-document transaction is needed.
/// </summary>
public interface IContentEngagementJourneyRepository
{
    Task<ContentEngagementJourney?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted journeys of a tenant (any status, archived included — history must stay readable).</summary>
    Task<IReadOnlyList<ContentEngagementJourney>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Non-deleted journeys carrying <paramref name="journeyCode"/> (any version/status). Backs the duplicate
    /// code+version guard (V-J03) and the overlapping-published guard (V-J10).</summary>
    Task<IReadOnlyList<ContentEngagementJourney>> ListByCodeAsync(
        Guid tenantId, string journeyCode, CancellationToken cancellationToken);

    Task InsertAsync(ContentEngagementJourney entity, CancellationToken cancellationToken);

    /// <summary>Version-checked single-document replace. Bumps <see cref="EntityBase.Version"/> from
    /// <paramref name="expectedVersion"/> to <c>expectedVersion + 1</c>; returns false when another writer already moved
    /// the token (controlled 409, no silent overwrite). Stage writes go through this same path (S2).</summary>
    Task<bool> ReplaceAsync(ContentEngagementJourney entity, int expectedVersion, CancellationToken cancellationToken);
}
