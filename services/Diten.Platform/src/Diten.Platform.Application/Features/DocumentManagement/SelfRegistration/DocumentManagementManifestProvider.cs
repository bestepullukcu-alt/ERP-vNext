using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementInstantiation;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;

namespace Diten.Platform.Application.Features.DocumentManagement.SelfRegistration;

/// <summary>
/// MC-3b-expand — the Document Management module self-registration manifest. Mirrors the REAL frontend
/// TenantShell view-routes of <c>DocumentManagementQmsBaselinesController</c> (QMS baseline list / import /
/// create-manual / details / designer) and <c>DocumentManagementInstantiationsController</c> (company adoption
/// list / details), with the verbatim <c>platform.document-management.*</c> keys the backend enforces. Every
/// RequiredPermission / PermissionKey below is a real <see cref="QmsBaselinePermissions"/>,
/// <see cref="DocumentManagementPermissions"/> or <see cref="DocumentManagementInstantiationPermissions"/>
/// constant (the same constants the API <c>[HasPermission]</c> attributes use) — zero drift. Two co-equal
/// top-level entries (QMS Baselines, Instantiations); the rest are sub-pages reached from a parent (nav=false).
/// SOFT fields (Domain/Service/DisplayName/SortOrder/IsTenantAssignable) are operator-owned after first seed.
/// </summary>
public sealed class DocumentManagementManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "document-management",
            ModuleName: "DocumentManagement",
            DisplayName: "Document Management",
            Domain: "DocumentManagement",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 310,
            Icon: "bx-file", // FIX-MODULE-ICON — default sidebar icon (SOFT; operator can override in catalog).
            Pages:
            [
                // ── QMS baselines (governance authoring) ──────────────────────────────────────────────────
                // List — the navigable entry. Toolbar Import / Create-Manual buttons NAVIGATE to their own
                // routes (modeled as pages below), so they are not actions; the list itself has none.
                new ModuleManifestPage(
                    PageCode: "QMS_BASELINES",
                    DisplayName: "QMS Baselines",
                    RoutePath: "/DocumentManagementQmsBaselines",
                    RequiredPermission: QmsBaselinePermissions.View,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: "QMS_IMPORT",
                    DisplayName: "Import QMS Baseline",
                    RoutePath: "/DocumentManagementQmsBaselines/Import",
                    RequiredPermission: QmsBaselinePermissions.Import,
                    ParentPageCode: "QMS_BASELINES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("IMPORT_DRY_RUN", "Preview Import", QmsBaselinePermissions.Import, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("IMPORT_COMMIT", "Commit Import", QmsBaselinePermissions.Import, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "QMS_CREATE_MANUAL",
                    DisplayName: "Create Manual Baseline",
                    RoutePath: "/DocumentManagementQmsBaselines/CreateManual",
                    RequiredPermission: QmsBaselinePermissions.Create,
                    ParentPageCode: "QMS_BASELINES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Baseline", QmsBaselinePermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "QMS_DETAILS",
                    DisplayName: "QMS Baseline Details",
                    RoutePath: "/DocumentManagementQmsBaselines/Details/{id}",
                    RequiredPermission: QmsBaselinePermissions.View,
                    ParentPageCode: "QMS_BASELINES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions:
                    [
                        new ModuleManifestAction("VALIDATE", "Validate Draft", QmsBaselinePermissions.Validate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PUBLISH", "Publish", QmsBaselinePermissions.Publish, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Node-editor: the collection-definition tree CRUD lives here (create/edit/move/delete + validate).
                new ModuleManifestPage(
                    PageCode: "QMS_DESIGNER",
                    DisplayName: "QMS Baseline Designer",
                    RoutePath: "/DocumentManagementQmsBaselines/Designer/{id}",
                    RequiredPermission: DocumentManagementPermissions.CollectionDefinitionsView,
                    ParentPageCode: "QMS_DETAILS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 14,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_DEFINITION", "Add Definition", QmsBaselinePermissions.CollectionDefinitionsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT_DEFINITION", "Edit Definition", QmsBaselinePermissions.CollectionDefinitionsEdit, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("MOVE_DEFINITION", "Move Definition", QmsBaselinePermissions.CollectionDefinitionsMove, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE_DEFINITION", "Delete Definition", QmsBaselinePermissions.CollectionDefinitionsDelete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VALIDATE", "Validate Draft", QmsBaselinePermissions.Validate, "Toolbar", 50, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // ── Instantiations (company adoption / provisioning) ──────────────────────────────────────
                new ModuleManifestPage(
                    PageCode: "INSTANCES",
                    DisplayName: "Document Instances",
                    RoutePath: "/DocumentManagementInstantiations",
                    RequiredPermission: DocumentManagementInstantiationPermissions.CollectionInstancesView,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("DRY_RUN", "Preview Instantiation", DocumentManagementInstantiationPermissions.InstantiationsDryRun, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EXECUTE", "Run Instantiation", DocumentManagementInstantiationPermissions.InstantiationsExecute, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RETRY", "Retry", DocumentManagementInstantiationPermissions.CollectionInstancesRetry, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", DocumentManagementInstantiationPermissions.InstantiationsExecute, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RESTORE", "Restore", DocumentManagementInstantiationPermissions.InstantiationsExecute, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: "INSTANCE_DETAILS",
                    DisplayName: "Document Instance Details",
                    RoutePath: "/DocumentManagementInstantiations/Details/{id}",
                    RequiredPermission: DocumentManagementInstantiationPermissions.CollectionInstancesView,
                    ParentPageCode: "INSTANCES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 21,
                    Actions: [])
            ]);
}
