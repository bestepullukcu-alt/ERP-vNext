namespace Diten.Platform.Application.Features.ModuleCatalog;

/// <summary>
/// Stable, machine-readable error codes for the module catalog feature.
/// Frontend, bu sabit anahtarları lokalize mesaja çevirir (örn. ModuleCodeInUse -> "Bu modül kodu zaten kullanılıyor").
/// </summary>
public static class ModuleCatalogErrorCodes
{
    public const string ModuleCodeInUse = "MODULE_CODE_IN_USE";
}
