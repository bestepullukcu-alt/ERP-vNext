using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountExternalReferenceRepository
{
    Task<bool> ExistsBySourceExternalAsync(
        Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountExternalReference>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    Task InsertAsync(AccountExternalReference reference, CancellationToken cancellationToken);
}
