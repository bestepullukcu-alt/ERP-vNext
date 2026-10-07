using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.VisitScope;

/// <summary>
/// WP-VP-2B — an in-memory account–contact link store that COUNTS its round trips. Its page read deliberately ignores
/// the tenant (<see cref="ListByAccountIdsAsync"/> matches on account id only) so a test proves the application-level
/// tenant guard, not the store's; every other read is tenant-scoped like production.
/// </summary>
internal sealed class InMemoryAccountContactLinks : IAccountContactLinkRepository
{
    public List<AccountContactLink> Items { get; } = new();
    public int PageReads { get; private set; }
    public int PerAccountReads { get; private set; }
    public int SetReads { get; private set; }

    private IEnumerable<AccountContactLink> Live(Guid tenantId) => Items.Where(l => l.TenantId == tenantId && !l.IsDeleted);

    public Task<IReadOnlyList<AccountContactLink>> ListByAccountIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        PageReads++;
        return Task.FromResult<IReadOnlyList<AccountContactLink>>(
            Items.Where(l => !l.IsDeleted && accountIds.Contains(l.AccountId)).ToList()); // tenant NOT filtered on purpose
    }

    public Task<IReadOnlyCollection<Guid>> ListAccountIdsWithActiveLinksAsync(
        Guid tenantId, IReadOnlyCollection<Guid> excludedContactIds, CancellationToken cancellationToken)
    {
        SetReads++;
        return Task.FromResult<IReadOnlyCollection<Guid>>(Live(tenantId)
            .Where(l => RelationshipLifecycle.IsActiveLink(l, !excludedContactIds.Contains(l.ContactId)))
            .Select(l => l.AccountId)
            .ToHashSet());
    }

    public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
    {
        PerAccountReads++;
        return Task.FromResult<IReadOnlyList<AccountContactLink>>(Live(tenantId).Where(l => l.AccountId == accountId).ToList());
    }

    public Task<AccountContactLink?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Live(tenantId).FirstOrDefault(l => l.Id == id));
    public Task<bool> ExistsActiveAsync(Guid tenantId, Guid accountId, Guid contactId, string roleCode, Guid? excludeId, CancellationToken ct)
        => Task.FromResult(false);
    public Task<bool> ExistsPrimaryAsync(Guid tenantId, Guid accountId, string roleCode, Guid? excludeId, CancellationToken ct)
        => Task.FromResult(false);
    public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AccountContactLink>>(Live(tenantId).Where(l => l.ContactId == contactId).ToList());
    public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AccountContactLink>>(Live(tenantId).ToList());
    public Task InsertAsync(AccountContactLink link, CancellationToken ct) { Items.Add(link); return Task.CompletedTask; }
    public Task UpdateAsync(AccountContactLink link, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>WP-VP-2B — an in-memory contact store (soft delete honoured) that counts its id reads.</summary>
internal sealed class InMemoryContacts : IContactRepository
{
    public List<Contact> Items { get; } = new();
    public int IdReads { get; private set; }

    public Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted));

    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        IdReads++;
        return Task.FromResult<IReadOnlyList<Contact>>(
            Items.Where(c => c.TenantId == tenantId && !c.IsDeleted && ids.Contains(c.Id)).ToList());
    }

    public Task<IReadOnlyCollection<Guid>> ListDeletedIdsAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>>(Items.Where(c => c.TenantId == tenantId && c.IsDeleted).Select(c => c.Id).ToList());

    public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct)
        => throw new NotSupportedException();
    public Task<IReadOnlyList<Contact>> ListAllAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Items.Where(c => c.TenantId == tenantId && !c.IsDeleted).ToList());
    public Task InsertAsync(Contact contact, CancellationToken ct) { Items.Add(contact); return Task.CompletedTask; }
    public Task UpdateAsync(Contact contact, CancellationToken ct) => Task.CompletedTask;
}
