using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>WP-SB-3b — <see cref="JourneyProgress"/> store. Tenant scoped; no delete. Every read is bounded by the tenant.</summary>
public interface IJourneyProgressRepository
{
    Task<JourneyProgress?> GetByKeyAsync(
        Guid tenantId, Guid contactId, Guid productId, Guid journeyId, CancellationToken cancellationToken);

    /// <summary>All of a doctor's progress rows (every product, every journey).</summary>
    Task<IReadOnlyList<JourneyProgress>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>Version-checked upsert: <paramref name="expectedVersion"/> = 0 inserts a new key (false when the key
    /// already exists — another writer was first); otherwise replaces the row only while its token is
    /// <paramref name="expectedVersion"/>. The token becomes <c>expectedVersion + 1</c>.</summary>
    Task<bool> UpsertAsync(JourneyProgress entity, int expectedVersion, CancellationToken cancellationToken);
}
