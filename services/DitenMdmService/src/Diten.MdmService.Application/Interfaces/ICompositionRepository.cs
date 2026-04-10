using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for composition (formulation) operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ICompositionRepository
{
    Task<Composition> CreateAsync(Composition entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Composition entity, CancellationToken cancellationToken = default);
    Task<Composition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Composition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string formulationCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
