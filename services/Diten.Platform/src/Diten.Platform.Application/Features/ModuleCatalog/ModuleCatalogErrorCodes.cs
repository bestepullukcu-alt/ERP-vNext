namespace Diten.Platform.Application.Features.ModuleCatalog;

/// <summary>
/// Stable, machine-readable error codes for the module catalog feature.
/// Frontend, bu sabit anahtarları lokalize mesaja çevirir (örn. ModuleCodeInUse -> "Bu modül kodu zaten kullanılıyor").
/// </summary>
public static class ModuleCatalogErrorCodes
{
    public const string ModuleCodeInUse = "MODULE_CODE_IN_USE";

    // MC-4 — the target module is code-owned (self-registered from a manifest); manual create/update/delete is refused.
    public const string ModuleManagedByCode = "MODULE_MANAGED_BY_CODE";

    // FIX-BASELINE-NO-DEACTIVATE — a baseline module reaches EVERY tenant automatically; deactivating (or moving it
    // off Active) would break RBAC/settings for all tenants and a re-push won't restore SOFT Status. Refused.
    public const string BaselineCannotBeDeactivated = "BASELINE_CANNOT_BE_DEACTIVATED";
}
