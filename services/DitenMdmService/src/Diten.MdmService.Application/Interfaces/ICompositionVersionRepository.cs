using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for composition version operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ICompositionVersionRepository : IRepository<CompositionVersion>
{
    // CompositionVersion-specific methods only — standard CRUD inherited from IRepository<CompositionVersion>
    Task<IReadOnlyList<CompositionVersion>> GetByCompositionIdAsync(Guid compositionId, CancellationToken ct = default);
    Task<CompositionVersion?> GetCurrentVersionAsync(Guid compositionId, CancellationToken ct = default);
    Task<int> GetNextVersionNoAsync(Guid compositionId, CancellationToken ct = default);
    Task<bool> MarkOtherVersionsAsSupersededAsync(Guid compositionId, Guid activeVersionId, CancellationToken ct = default);
}
