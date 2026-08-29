using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Contact> Items, long Total)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>All active contacts for the tenant (export). Soft-deleted excluded.</summary>
    Task<IReadOnlyList<Contact>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(Contact contact, CancellationToken cancellationToken);

    Task UpdateAsync(Contact contact, CancellationToken cancellationToken);
}
