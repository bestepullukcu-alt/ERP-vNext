using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.TenantSettingsModule.SelfRegistration;

/// <summary>
/// FEAT-BASELINE-MODULES-S2 — the Tenant Settings module self-registration manifest, a BASELINE module
/// (<c>IsBaseline = true</c>): entitlement-free, so every tenant automatically has access. The per-user permission
/// gate is preserved: both pages carry the verbatim <c>platform.tenant-security.manage</c> key the previous
/// hardcoded tenant-shell block gated on (tenant Admins hold it via the baseline role template), so a user only
/// sees the links when permitted (identical to before, now rendered data-driven under the "Settings" domain).
/// <para>The two pages mirror the REAL tenant self-service routes: /TenantSecurity (Security Settings) and
/// /TenantNavigation (Menu Settings). Co-equal top-level nav entries, each IsNavigationVisible.</para>
/// </summary>
public sealed class TenantSettingsManifestProvider : IModuleManifestProvider
{
    // Verbatim permission key (same key the previous hardcoded _LayoutTenantShell Security/Menu block gated on).
    private const string TenantSecurityManage = "platform.tenant-security.manage";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "tenant-settings",
            ModuleName: "Tenant Settings",
            DisplayName: "Tenant Settings",
            Domain: "Administration", // FEAT-ADMIN-DOMAIN — grouped with Access Governance under one Administration domain.
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 900, // settings sit at the bottom of the sidebar
            Icon: "bx-cog",
            IsBaseline: true, // FEAT-BASELINE-MODULES — entitlement-free; every tenant gets it.
            Pages:
            [
                new ModuleManifestPage("SECURITY_SETTINGS", "Security Settings", "/TenantSecurity", TenantSecurityManage, null, true, "List", 10, []),
                new ModuleManifestPage("MENU_SETTINGS", "Menu Settings", "/TenantNavigation", TenantSecurityManage, null, true, "List", 20, [])
            ]);
}
