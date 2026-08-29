using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0150 FU07 date-specific availability exceptions. Like the availability master there is <b>no delete
/// method</b> — closing is an update to inactive/archived.
/// </summary>
public interface IContactAvailabilityExceptionRepository
{
    Task<ContactAvailabilityException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactAvailabilityException>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactAvailabilityException>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactAvailabilityException>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>Exceptions for a set of links (lookup fan-in). Fan-out default; Mongo overrides with <c>$in</c>.</summary>
    async Task<IReadOnlyList<ContactAvailabilityException>> ListByLinkIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> linkIds, CancellationToken cancellationToken)
    {
        if (linkIds is null || linkIds.Count == 0)
        {
            return [];
        }

        var result = new List<ContactAvailabilityException>();
        foreach (var linkId in linkIds.Distinct())
        {
            result.AddRange(await ListByLinkAsync(tenantId, linkId, cancellationToken));
        }

        return result;
    }

    Task InsertAsync(ContactAvailabilityException exception, CancellationToken cancellationToken);

    Task UpdateAsync(ContactAvailabilityException exception, CancellationToken cancellationToken);
}
