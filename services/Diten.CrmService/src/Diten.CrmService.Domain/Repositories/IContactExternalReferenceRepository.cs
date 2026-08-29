using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IContactExternalReferenceRepository
{
    Task<bool> ExistsBySourceExternalAsync(
        Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>All active external references for the tenant (workbook export) — avoids a per-contact round trip.</summary>
    Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Resolve an active external reference by (SourceSystem, ExternalId) — import contact lookup.</summary>
    Task<ContactExternalReference?> GetBySourceExternalAsync(Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken);

    Task InsertAsync(ContactExternalReference reference, CancellationToken cancellationToken);
}
