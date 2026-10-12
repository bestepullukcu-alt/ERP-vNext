using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountRelationshipRepository
{
    Task<AccountRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Whether an active relationship with the same type already exists between the two accounts. When
    /// <paramref name="includeReverse"/> is true (bidirectional types) the reverse pair (Target→Source) also counts.
    /// </summary>
    Task<bool> ExistsActivePairAsync(
        Guid tenantId, Guid sourceAccountId, Guid targetAccountId, string relationshipType, bool includeReverse, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>All active relationships where the account is either source or target (for Account 360).</summary>
    Task<IReadOnlyList<AccountRelationship>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>All active relationships for the tenant (export).</summary>
    /// <summary>WP-VP-3D (D5) — every non-deleted relationship touching one of <paramref name="accountIds"/> (as source
    /// OR target) in ONE read: the bulk twin of <see cref="ListByAccountAsync"/>. The default loops it; the Mongo
    /// repository answers with a single <c>$in</c> find.</summary>
    async Task<IReadOnlyList<AccountRelationship>> ListByAccountIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds is null || accountIds.Count == 0) return Array.Empty<AccountRelationship>();
        var result = new Dictionary<Guid, AccountRelationship>();
        foreach (var accountId in accountIds.Distinct())
        {
            foreach (var r in await ListByAccountAsync(tenantId, accountId, cancellationToken))
            {
                result[r.Id] = r;
            }
        }

        return result.Values.ToList();
    }

    Task<IReadOnlyList<AccountRelationship>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(AccountRelationship relationship, CancellationToken cancellationToken);

    Task UpdateAsync(AccountRelationship relationship, CancellationToken cancellationToken);
}
