using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IModulePageActionDescriptorRepository
{
    Task<ModulePageActionDescriptor> CreateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default);
    Task<ModulePageActionDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByActionCodeAsync(Guid pageDescriptorId, string actionCode, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ModulePageActionDescriptor>> GetByPageAsync(Guid pageDescriptorId, CancellationToken ct = default);

    /// <summary>
    /// FEAT-CATALOG-PERM-DELETE-SYNC — count of LIVE actions whose <c>PermissionKey</c> equals the key (used to
    /// decide whether a permission is still referenced before requesting its removal from AuthService).
    /// </summary>
    Task<long> CountByPermissionKeyAsync(string permissionKey, CancellationToken ct = default);
}
