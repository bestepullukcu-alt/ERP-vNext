using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.WorkAggregation.SelfRegistration;

/// <summary>
/// WC-1b (DCP-004 §8 row 1b) — the Görev Merkezi / Task Center self-registration manifest. Without it the module
/// has no catalog entry, no tenant nav entry, and no permission/entitlement chain.
/// <para>The single page mirrors the REAL tenant route <c>/WorkCenterNext</c> and carries the verbatim
/// <see cref="WorkAggregationPermissions.InboxView"/> constant the WC-1 controller enforces (zero drift — the key
/// is referenced, never re-typed). ModuleCode stays a clean slug ("work-aggregation"); the governance identity
/// is never written into runtime code.</para>
/// <para><c>IsBaseline = false</c> + <c>IsTenantAssignable = true</c> (pack DEC-4): the module is entitlement-gated,
/// so the tenant Admin grant flows through the entitlement→permission bridge rather than a hand-edit inside the
/// protected AuthService. Consequence: the module stays invisible until an operator entitles it to the tenant.</para>
/// <para>No actions are declared: WorkCenter executes NO commands in this slice — approve/reject/delegate remain
/// on the MOD-0023 workflow endpoints (read/projection only).</para>
/// SOFT fields (Domain/Service/DisplayName/SortOrder/IsTenantAssignable/Icon) are operator-owned after first seed.
/// </summary>
public sealed class WorkAggregationManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "work-aggregation",
            ModuleName: "Work Aggregation",
            DisplayName: "Görev Merkezi / Task Center",
            // Pack DEC-5 — a dedicated domain: no existing domain fits a personal work surface. The domain row is
            // operator-managed (platform_module_domains); an unresolved code falls back to the code itself and is
            // localized in the sidebar via the stable Nav.Domain.WORKSPACE key.
            Domain: "Workspace",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 10, // personal entry point — top of the sidebar
            Icon: "bx-been-here", // matches the icon the previous hardcoded tenant-shell entry used
            IsBaseline: false, // pack DEC-4 — entitlement-gated, NOT baseline
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "WORKCENTER",
                    DisplayName: "Görev Merkezi",
                    RoutePath: "/WorkCenterNext",
                    RequiredPermission: WorkAggregationPermissions.InboxView,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions: [])
            ]);
}
