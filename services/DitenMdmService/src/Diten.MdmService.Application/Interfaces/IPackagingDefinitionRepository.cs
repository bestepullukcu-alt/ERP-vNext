using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for packaging definition operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface IPackagingDefinitionRepository : IRepository<PackagingDefinition>
{
    // PackagingDefinition-specific methods only — standard CRUD inherited from IRepository<PackagingDefinition>
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
}
