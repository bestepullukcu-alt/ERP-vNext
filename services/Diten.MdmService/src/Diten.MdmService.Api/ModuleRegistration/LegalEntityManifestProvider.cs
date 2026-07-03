using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// MC-3b-expand (Part B) — the Legal Entity (MOD-0220, MasterDataManagement) self-registration manifest. Mirrors the
/// REAL frontend <c>LegalEntitiesController</c> view-routes (list + full-page create wizard + edit wizard + details)
/// and the verbatim <c>mdm.legal-entities.*</c> keys the MDM API enforces (<c>LegalEntitiesController</c> [HasPermission]) —
/// zero drift. §2a: the 8-step wizard is presentation within ONE route, so it is a SINGLE page (steps are NOT pages);
/// create (/Wizard) and edit (/Wizard/{id}) are distinct routes → one page each. Lifecycle (activate/suspend/archive)
/// all gate on mdm.legal-entities.update (the real API permission). SOFT fields are operator-owned after first seed.
/// </summary>
public sealed class LegalEntityManifestProvider : IModuleManifestProvider
{
    // Verbatim [HasPermission] keys (raw strings — the MDM controller gates by literal).
    private const string Read = "mdm.legal-entities.read";
    private const string Create = "mdm.legal-entities.create";
    private const string Update = "mdm.legal-entities.update";
    private const string Delete = "mdm.legal-entities.delete";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "legal-entity",
            ModuleName: "LegalEntity",
            DisplayName: "Legal Entity",
            Domain: "MasterDataManagement",
            Service: "DitenMdmService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 330,
            Icon: "bx-buildings", // FIX-MODULE-ICON — default sidebar icon (SOFT; operator can override in catalog).
            Pages:
            [
                // List — the navigable entry. Add-New / Edit / Quick View / Details are navigation (to the wizard
                // and details pages, or a read-only offcanvas), so the actions here are the lifecycle + delete that
                // run via AJAX on the row menu.
                new ModuleManifestPage(
                    PageCode: "LEGAL_ENTITIES",
                    DisplayName: "Legal Entities",
                    RoutePath: "/LegalEntities",
                    RequiredPermission: Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("ACTIVATE", "Activate", Update, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SUSPEND", "Suspend", Update, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", Update, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", Delete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Full-page 8-step CREATE wizard (one route; steps are presentation, not pages — §2a).
                new ModuleManifestPage(
                    PageCode: "WIZARD_CREATE",
                    DisplayName: "Create Legal Entity",
                    RoutePath: "/LegalEntities/Wizard",
                    RequiredPermission: Create,
                    ParentPageCode: "LEGAL_ENTITIES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Legal Entity", Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full-page EDIT wizard (distinct route; same view, edit mode).
                new ModuleManifestPage(
                    PageCode: "WIZARD_EDIT",
                    DisplayName: "Edit Legal Entity",
                    RoutePath: "/LegalEntities/Wizard/{id}",
                    RequiredPermission: Update,
                    ParentPageCode: "LEGAL_ENTITIES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Legal Entity", Update, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full details page (all 8 sections) with manual lifecycle action buttons. Edit navigates to the
                // wizard (a page), so the actions here are the lifecycle transitions.
                new ModuleManifestPage(
                    PageCode: "DETAILS",
                    DisplayName: "Legal Entity Details",
                    RoutePath: "/LegalEntities/Details/{id}",
                    RequiredPermission: Read,
                    ParentPageCode: "LEGAL_ENTITIES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions:
                    [
                        new ModuleManifestAction("ACTIVATE", "Activate", Update, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SUSPEND", "Suspend", Update, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("ARCHIVE", "Archive", Update, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ]);
}
