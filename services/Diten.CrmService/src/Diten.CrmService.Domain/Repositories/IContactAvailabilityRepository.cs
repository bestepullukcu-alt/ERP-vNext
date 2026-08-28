using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0150 FU07 availability master. Reads are link/contact/account scoped; there is deliberately <b>no delete
/// method</b> — closing a row is an update to inactive/archived.
/// </summary>
public interface IContactAvailabilityRepository
{
    Task<ContactAvailability?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All rows (any status) of one AccountContactLink.</summary>
    Task<IReadOnlyList<ContactAvailability>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken);

    /// <summary>All rows (any status) across every link of one contact.</summary>
    Task<IReadOnlyList<ContactAvailability>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>All rows (any status) of every contact at one account/location.</summary>
    Task<IReadOnlyList<ContactAvailability>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>Rows for a set of links (lookup fan-in). Defaults to per-link fan-out so alternate implementations
    /// (tests) work unchanged; the Mongo repository overrides it with a single <c>$in</c> query.</summary>
    async Task<IReadOnlyList<ContactAvailability>> ListByLinkIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> linkIds, CancellationToken cancellationToken)
    {
        if (linkIds is null || linkIds.Count == 0)
        {
            return [];
        }

        var result = new List<ContactAvailability>();
        foreach (var linkId in linkIds.Distinct())
        {
            result.AddRange(await ListByLinkAsync(tenantId, linkId, cancellationToken));
        }

        return result;
    }

    Task InsertAsync(ContactAvailability availability, CancellationToken cancellationToken);

    Task UpdateAsync(ContactAvailability availability, CancellationToken cancellationToken);
}
