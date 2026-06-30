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
            Pages:
            [
                // Organization Units — slim list (toolbar Add + row Edit/Archive/Delete).
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
                        new ModuleManifestAction("CREATE", "Create Organization Unit", OrgUnitsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit", OrgUnitsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", OrgUnitsArchive, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", OrgUnitsDelete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Positions — slim list (toolbar Add + row Edit/Archive/Delete). manager-chain is a read-only
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
                        new ModuleManifestAction("CREATE", "Create Position", PositionsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit", PositionsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", PositionsArchive, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", PositionsDelete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Position Assignments — slim list. Unlike the others there is NO archive endpoint, so no Archive action.
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
                        new ModuleManifestAction("CREATE", "Create Position Assignment", AssignmentsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit", AssignmentsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", AssignmentsDelete, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ]);
}
