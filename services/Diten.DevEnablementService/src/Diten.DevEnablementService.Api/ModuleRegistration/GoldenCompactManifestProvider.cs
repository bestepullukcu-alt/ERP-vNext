using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.DevEnablementService.Api.ModuleRegistration;

/// <summary>
/// Golden Compact (DevEnablement) self-registration manifest. Mirrors the ACTUAL code: the full-page CRUD pattern of
/// <c>GoldenReferenceCompactController</c> (frontend) — List + Create + Edit + Details routes — and the verbatim
/// <c>goldencompact.*</c> keys the API <c>GoldenReferenceCompactController</c> enforces via [HasPermission]. Real UI
/// buttons → actions: the list's Export toolbar button (records.export) and Delete (row + bulk, records.delete);
/// Create/Edit save with records.{create,update}. Add-New / Edit / Quick View / Details are navigation (to other
/// pages or a read-only offcanvas), not actions. <c>goldencompact.reports.view</c> is an API-only capability (no
/// frontend route yet), so it is a system-wide permission but NOT a catalog page. (Zero drift to code.)
/// </summary>
public sealed class GoldenCompactManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "goldencompact",
            ModuleName: "Golden Reference Compact",
            DisplayName: "Golden Compact",
            Domain: "DevEnablement",
            Service: "DevEnablement",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 110,
            Pages:
            [
                // Records list — the navigable entry. Export + Delete run here; Add-New/Edit/Details navigate.
                new ModuleManifestPage(
                    PageCode: "RECORDS",
                    DisplayName: "Records",
                    RoutePath: "/GoldenReferenceCompact",
                    RequiredPermission: "goldencompact.records.read",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("EXPORT", "Export", "goldencompact.records.export", "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DELETE", "Delete", "goldencompact.records.delete", "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // Full-page create.
                new ModuleManifestPage(
                    PageCode: "CREATE",
                    DisplayName: "Create Record",
                    RoutePath: "/GoldenReferenceCompact/Create",
                    RequiredPermission: "goldencompact.records.create",
                    ParentPageCode: "RECORDS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save", "goldencompact.records.create", "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Full-page edit.
                new ModuleManifestPage(
                    PageCode: "EDIT",
                    DisplayName: "Edit Record",
                    RoutePath: "/GoldenReferenceCompact/Edit/{id}",
                    RequiredPermission: "goldencompact.records.update",
                    ParentPageCode: "RECORDS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save", "goldencompact.records.update", "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Read-only details — the Edit button navigates to the Edit page (so no action here).
                new ModuleManifestPage(
                    PageCode: "DETAILS",
                    DisplayName: "Record Details",
                    RoutePath: "/GoldenReferenceCompact/Details/{id}",
                    RequiredPermission: "goldencompact.records.read",
                    ParentPageCode: "RECORDS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions: [])
            ]);
}
