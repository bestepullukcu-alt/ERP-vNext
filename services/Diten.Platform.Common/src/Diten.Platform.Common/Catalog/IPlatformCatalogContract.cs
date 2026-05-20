namespace Diten.Platform.Common.Catalog;

public interface IPlatformCatalogContract
{
    Task<IReadOnlyList<AssignableModuleInfo>> GetAssignableModulesAsync(CancellationToken ct);
}
