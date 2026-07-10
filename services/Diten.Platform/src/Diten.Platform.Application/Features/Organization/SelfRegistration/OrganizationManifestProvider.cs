using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Organization.SelfRegistration;

/// <summary>
/// MC-3b-expand — the Organization (PlatformSharedServices) module self-registration manifest. Mirrors the REAL
/// frontend slim DataTable pages (OrganizationUnits / Positions / PositionAssignments — each an Index list with an
/// offcanvas create/edit + row actions) and the verbatim <c>platform.*</c> permission keys the backend
/// <c>[HasPermission]</c> enforces (OrganizationUnitsController / PositionsController / PositionAssignmentsController).
/// Permissions are declared as raw strings (these controllers gate by literal, not a *Permissions constant class);
/// the completeness test reflects the real enforced keys off the API controllers so the mirror stays zero-drift.
/// The three pages are co-equal top-level nav entries (no parent), so each is IsNavigationVisible. SOFT fields
/// (Domain/Service/DisplayName/SortOrder/IsTenantAssignable) are operator-owned after first seed.
/// </summary>
public sealed class OrganizationManifestProvider : IModuleManifestProvider
{
    // Verbatim [HasPermission] keys (raw strings — these controllers do not use a constant class).
    private const string OrgUnitsRead = "platform.organization-units.read";
    private const string OrgUnitsCreate = "platform.organization-units.create";
    private const string OrgUnitsUpdate = "platform.organization-units.update";
    private const string OrgUnitsArchive = "platform.organization-units.archive";
    private const string OrgUnitsDelete = "platform.organization-units.delete";

    private const string PositionsRead = "platform.positions.read";
    private const string PositionsCreate = "platform.positions.create";
    private const string PositionsUpdate = "platform.positions.update";
    private const string PositionsArchive = "platform.positions.archive";
    private const string PositionsDelete = "platform.positions.delete";

    private const string AssignmentsRead = "platform.position-assignments.read";
    private const string AssignmentsCreate = "platform.position-assignments.create";
    private const string AssignmentsUpdate = "platform.position-assignments.update";
    private const string AssignmentsDelete = "platform.position-assignments.delete";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "organization",
            ModuleName: "Organization",
            DisplayName: "Organization",
            Domain: "PlatformSharedServices",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 300,
            Icon: "bx-sitemap", // FIX-MODULE-ICON — default sidebar icon (SOFT; operator can override in catalog).
            Pages:
            [
                // MOD-0288 Phase 2 — Organization Units reshaped to full-page Create/Edit/Details (§2a: create &
                // edit are distinct routes → one page each; Add/Edit on the list now NAVIGATE to those pages, so the
                // list keeps only the AJAX row lifecycle actions). Positions/PositionAssignments remain slim (later slices).
                new ModuleManifestPage(
                    PageCode: "ORGANIZATION_UNITS",
                    DisplayName: "Organization Units",
                    RoutePath: "/OrganizationUnits",
                    RequiredPermission: OrgUnitsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("ARCHIVE", "Archive", OrgUnitsArchive, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", OrgUnitsDelete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Full-page CREATE form (compact two-level: basic + collapsible advanced).
                new ModuleManifestPage(
                    PageCode: "ORG_UNIT_CREATE",
                    DisplayName: "Create Organization Unit",
                    RoutePath: "/OrganizationUnits/Create",
                    RequiredPermission: OrgUnitsCreate,
                    ParentPageCode: "ORGANIZATION_UNITS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Organization Unit", OrgUnitsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full-page EDIT form (distinct route; same view, edit mode).
                new ModuleManifestPage(
                    PageCode: "ORG_UNIT_EDIT",
                    DisplayName: "Edit Organization Unit",
                    RoutePath: "/OrganizationUnits/Edit/{id}",
                    RequiredPermission: OrgUnitsUpdate,
                    ParentPageCode: "ORGANIZATION_UNITS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Organization Unit", OrgUnitsUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full details page (read view + lifecycle toolbar actions).
                new ModuleManifestPage(
                    PageCode: "ORG_UNIT_DETAILS",
                    DisplayName: "Organization Unit Details",
                    RoutePath: "/OrganizationUnits/Details/{id}",
                    RequiredPermission: OrgUnitsRead,
                    ParentPageCode: "ORGANIZATION_UNITS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions:
                    [
                        new ModuleManifestAction("ARCHIVE", "Archive", OrgUnitsArchive, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DELETE", "Delete", OrgUnitsDelete, "Toolbar", 20, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // MOD-0288 Phase 3 — Positions reshaped to full-page Create/Edit/Details (§2a). Add/Edit navigate to
                // those pages, so the list keeps only the AJAX row lifecycle actions. manager-chain is a read-only
                // lookup feed (no button), so it is NOT modeled as an action.
                new ModuleManifestPage(
                    PageCode: "POSITIONS",
                    DisplayName: "Positions",
                    RoutePath: "/Positions",
                    RequiredPermission: PositionsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("ARCHIVE", "Archive", PositionsArchive, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", PositionsDelete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Full-page CREATE form (compact two-level).
                new ModuleManifestPage(
                    PageCode: "POSITION_CREATE",
                    DisplayName: "Create Position",
                    RoutePath: "/Positions/Create",
                    RequiredPermission: PositionsCreate,
                    ParentPageCode: "POSITIONS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 21,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Position", PositionsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full-page EDIT form.
                new ModuleManifestPage(
                    PageCode: "POSITION_EDIT",
                    DisplayName: "Edit Position",
                    RoutePath: "/Positions/Edit/{id}",
                    RequiredPermission: PositionsUpdate,
                    ParentPageCode: "POSITIONS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 22,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Position", PositionsUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full details page (read view + manager chain + lifecycle toolbar actions).
                new ModuleManifestPage(
                    PageCode: "POSITION_DETAILS",
                    DisplayName: "Position Details",
                    RoutePath: "/Positions/Details/{id}",
                    RequiredPermission: PositionsRead,
                    ParentPageCode: "POSITIONS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 23,
                    Actions:
                    [
                        new ModuleManifestAction("ARCHIVE", "Archive", PositionsArchive, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DELETE", "Delete", PositionsDelete, "Toolbar", 20, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // MOD-0288 Phase 4 — Position Assignments reshaped to full-page Create/Edit/Details (§2a). Add/Edit
                // navigate to those pages; the list keeps only the AJAX delete row action. There is NO archive
                // endpoint for assignments, so (unlike Org Unit / Position) no Archive action anywhere.
                new ModuleManifestPage(
                    PageCode: "POSITION_ASSIGNMENTS",
                    DisplayName: "Position Assignments",
                    RoutePath: "/PositionAssignments",
                    RequiredPermission: AssignmentsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("DELETE", "Delete", AssignmentsDelete, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Full-page CREATE form (compact two-level).
                new ModuleManifestPage(
                    PageCode: "ASSIGNMENT_CREATE",
                    DisplayName: "Create Position Assignment",
                    RoutePath: "/PositionAssignments/Create",
                    RequiredPermission: AssignmentsCreate,
                    ParentPageCode: "POSITION_ASSIGNMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 31,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Position Assignment", AssignmentsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full-page EDIT form.
                new ModuleManifestPage(
                    PageCode: "ASSIGNMENT_EDIT",
                    DisplayName: "Edit Position Assignment",
                    RoutePath: "/PositionAssignments/Edit/{id}",
                    RequiredPermission: AssignmentsUpdate,
                    ParentPageCode: "POSITION_ASSIGNMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 32,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Position Assignment", AssignmentsUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full details page (read view + derived status + delete). No archive endpoint for assignments.
                new ModuleManifestPage(
                    PageCode: "ASSIGNMENT_DETAILS",
                    DisplayName: "Position Assignment Details",
                    RoutePath: "/PositionAssignments/Details/{id}",
                    RequiredPermission: AssignmentsRead,
                    ParentPageCode: "POSITION_ASSIGNMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 33,
                    Actions:
                    [
                        new ModuleManifestAction("DELETE", "Delete", AssignmentsDelete, "Toolbar", 10, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ])
            ]);
}
