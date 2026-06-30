namespace Diten.Platform.Domain.Enums;

// MC-4 — how a catalog item entered the catalog. DEFAULT Manual so existing rows (no field) read back as Manual.
public enum ModuleCatalogOrigin
{
    Manual = 0,
    SelfRegistered = 1
}
