using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.WorkAggregation.SelfRegistration;

/// <summary>
/// WC-1b (DCP-004 §8 row 1b) — the Görev Merkezi / Task Center self-registration manifest. Without it the module
/// has no catalog entry and no tenant nav entry. ModuleCode stays a clean slug ("work-aggregation"); the governance
/// identity is never written into runtime code.
///
/// <para><b>Every tenant user's surface (DCP-004 "Decision amendment 2026-09-15", BL-410).</b> The module is
/// <c>IsBaseline = true</c> (entitlement-free) and its page carries NO RequiredPermission, so the sidebar and Ctrl+K
/// show Görev Merkezi to every tenant user. <c>IsTenantAssignable</c> stays true: the tenant menu reads only
/// assignable catalog rows. What a user may DO there is unchanged — every action is the source module's own key,
/// checked by the server when it is pressed. This supersedes pack DEC-4 (entitlement-gated).</para>
///
/// <para><b><see cref="WorkAggregationPermissions.InboxView"/> stays DECLARED, not enforced.</b> No endpoint asks for
/// it any more. It is kept on one non-UI "View" action (not a toolbar or row action, and named with the page's own
/// display name, so no new text) so the catalog→Auth sync and the entitled-module key pull keep treating it as this
/// module's key until delete-sync exists — the amendment's "stays in the catalog unenforced".</para>
///
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
            IsBaseline: true, // DCP-004 amendment 2026-09-15 — every tenant user; supersedes pack DEC-4
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "WORKCENTER",
                    DisplayName: "Görev Merkezi",
                    RoutePath: "/WorkCenterNext",
                    // No key: the page is every tenant user's personal inbox (the reconcile stores blank as null).
                    RequiredPermission: string.Empty,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        // Declaration only, not a button: see the class note.
                        new ModuleManifestAction(
                            ActionCode: "INBOX_VIEW",
                            DisplayName: "Görev Merkezi",
                            PermissionKey: WorkAggregationPermissions.InboxView,
                            ActionType: "View",
                            SortOrder: 10,
                            IsDangerous: false,
                            IsToolbarAction: false,
                            IsRowAction: false)
                    ])
            ]);
}
