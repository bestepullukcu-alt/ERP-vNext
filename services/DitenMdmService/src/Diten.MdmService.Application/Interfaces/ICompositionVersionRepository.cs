using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for composition version operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ICompositionVersionRepository
{
    Task<CompositionVersion> CreateAsync(CompositionVersion entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CompositionVersion entity, CancellationToken cancellationToken = default);
    Task<CompositionVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompositionVersion>> GetByCompositionIdAsync(Guid compositionId, CancellationToken cancellationToken = default);
    Task<CompositionVersion?> GetCurrentVersionAsync(Guid compositionId, CancellationToken cancellationToken = default);
    Task<int> GetNextVersionNoAsync(Guid compositionId, CancellationToken cancellationToken = default);
    Task<bool> MarkOtherVersionsAsSupersededAsync(Guid compositionId, Guid activeVersionId, CancellationToken cancellationToken = default);
}
