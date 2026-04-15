using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for composition (formulation) operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ICompositionRepository : IRepository<Composition>
{
    // Composition-specific methods only — standard CRUD inherited from IRepository<Composition>
    Task<bool> ExistsByCodeAsync(string formulationCode, Guid? excludeId = null, CancellationToken ct = default);
}
