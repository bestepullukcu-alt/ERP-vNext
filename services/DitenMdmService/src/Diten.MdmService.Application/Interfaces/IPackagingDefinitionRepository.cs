using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

public interface IPackagingDefinitionRepository
{
    Task<PackagingDefinition> CreateAsync(PackagingDefinition entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PackagingDefinition entity, CancellationToken cancellationToken = default);
    Task<PackagingDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackagingDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
