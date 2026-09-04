using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Batch point-read: the active contacts for the given ids (order unspecified; missing ids simply absent).
    /// Used to resolve display names / specialties for a set of planned visits in one round-trip.</summary>
    Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Server-side paged list. <paramref name="sortBy"/> accepts "displayName"/"contactType" (both backed
    /// by a {TenantId, field} index so descending stays an index scan, never a 32MB in-memory sort); any other value
    /// falls back to DisplayName ascending. <paramref name="statuses"/>/<paramref name="contactTypes"/> are cheap
    /// stored-field IN predicates. Returns the filtered <c>Total</c> plus the tenant-wide <c>UnfilteredTotal</c>
    /// (search + chip filters ignored) for DataTables recordsTotal.</summary>
    Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken cancellationToken);

    /// <summary>All active contacts for the tenant (export). Soft-deleted excluded.</summary>
    Task<IReadOnlyList<Contact>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(Contact contact, CancellationToken cancellationToken);

    Task UpdateAsync(Contact contact, CancellationToken cancellationToken);
}
