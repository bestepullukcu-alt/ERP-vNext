using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountContactLinkRepository
{
    Task<AccountContactLink?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Active (non-deleted) link with the same natural key — used to enforce duplicate (409).</summary>
    Task<bool> ExistsActiveAsync(
        Guid tenantId, Guid accountId, Guid contactId, string roleCode, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Whether an active primary link already exists for (Account, Role) — used to enforce primary uniqueness (409).</summary>
    Task<bool> ExistsPrimaryAsync(
        Guid tenantId, Guid accountId, string roleCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>Active links for a set of contacts (contact-list grid enrichment). Production overrides this with a
    /// single <c>$in</c> query; the default fans out to <see cref="ListByContactAsync"/> so alternate implementations
    /// (tests) work without change.</summary>
    async Task<IReadOnlyList<AccountContactLink>> ListByContactIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken)
    {
        if (contactIds is null || contactIds.Count == 0) return [];
        var result = new List<AccountContactLink>();
        foreach (var contactId in contactIds.Distinct())
        {
            result.AddRange(await ListByContactAsync(tenantId, contactId, cancellationToken));
        }
        return result;
    }

    /// <summary>All active links for the tenant (export).</summary>
    Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(AccountContactLink link, CancellationToken cancellationToken);

    Task UpdateAsync(AccountContactLink link, CancellationToken cancellationToken);
}
