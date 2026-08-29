using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountAttributeValueRepository
{
    Task<IReadOnlyList<AccountAttributeValue>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>Insert or update the value for (TenantId, AccountId, AttributeCode).</summary>
    Task UpsertAsync(AccountAttributeValue attribute, CancellationToken cancellationToken);
}
