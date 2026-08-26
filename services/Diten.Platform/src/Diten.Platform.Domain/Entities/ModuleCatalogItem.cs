using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class ModuleCatalogItem : GlobalEntity
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    // FU16: immutable producer binding. Registration may set this once from trusted server-side sender context only.
    public string? ProducerOwnerCode { get; init; }
    public ModuleCatalogStatus Status { get; set; } = ModuleCatalogStatus.Draft;
    public string ModuleVersion { get; set; } = "1.0.0";
    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; }
    public int SortOrder { get; set; }

    // FEAT-BASELINE-MODULES — HARD (code-owned via manifest): a baseline module is entitlement-free — every tenant
    // automatically has access (the tenant entitlement check is bypassed; per-user auth.* page gate still applies).
    // Default false → legacy docs deserialize as non-baseline (existing entitlement behaviour preserved).
    public bool IsBaseline { get; set; }

    // FIX-MODULE-ICON — sidebar icon as a boxicons class (e.g. "bx-cog"). SOFT metadata: seeded once from the
    // module's self-registration manifest, thereafter operator-owned via the catalog edit form (re-push never
    // overwrites). Null → the navigation stamps a "bx-box" fallback. Legacy docs deserialize to null (no regression).
    public string? Icon { get; set; }

    // MC-4 — Manual (operator-added) vs SelfRegistered (code-owned via manifest). Default Manual: legacy docs with
    // no Origin field deserialize to Manual, preserving existing behaviour.
    public ModuleCatalogOrigin Origin { get; set; } = ModuleCatalogOrigin.Manual;
}
