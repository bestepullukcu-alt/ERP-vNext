using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

public interface IOrganizationUnitRepository
{
    Task<OrganizationUnit> CreateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default);
    Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
