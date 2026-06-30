using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.ModulePages;

/// <summary>
/// MC-7 — a self-registered (Origin=SelfRegistered) module's pages and actions are code-owned (HARD): they are
/// reconciled from the manifest every startup and orphan manual entries are pruned (MC-6). So manual page/action
/// create/update/delete on such a module is refused (409 <see cref="ModuleCatalog.ModuleCatalogErrorCodes.ModuleManagedByCode"/>),
/// extending the MC-4 module-level guard down to descriptors. Manual modules are unaffected.
/// </summary>
internal static class SelfRegisteredModuleGuard
{
    public static async Task<bool> IsManagedByCodeAsync(
        IModuleCatalogRepository catalogRepository,
        string moduleCode,
        CancellationToken ct)
    {
        var module = await catalogRepository.GetByCodeAsync(moduleCode, ct);
        return module is not null && module.Origin == ModuleCatalogOrigin.SelfRegistered;
    }
}
