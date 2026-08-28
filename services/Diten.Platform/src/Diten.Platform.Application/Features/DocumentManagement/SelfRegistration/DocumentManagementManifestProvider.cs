using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementInstantiation;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants;

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
                    Actions: []),

                // ── Controlled Documents (folder-scoped controlled-document register) ─────────────────────
                new ModuleManifestPage(
                    PageCode: "CONTROLLED_DOCUMENTS",
                    DisplayName: "Controlled Documents",
                    RoutePath: "/DocumentManagementControlledDocuments",
                    RequiredPermission: DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "New Controlled Document", DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SHARE", "Share", DocumentManagementControlledDocumentsPermissions.ControlledDocumentsShare, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: "CONTROLLED_DOCUMENTS_CREATE",
                    DisplayName: "New Controlled Document",
                    RoutePath: "/DocumentManagementControlledDocuments/Create",
                    RequiredPermission: DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate,
                    ParentPageCode: "CONTROLLED_DOCUMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 31,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Document", DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "CONTROLLED_DOCUMENTS_DETAILS",
                    DisplayName: "Controlled Document Details",
                    RoutePath: "/DocumentManagementControlledDocuments/Details/{id}",
                    RequiredPermission: DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView,
                    ParentPageCode: "CONTROLLED_DOCUMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 32,
                    Actions:
                    [
                        new ModuleManifestAction("NEW_VERSION", "Upload New Version", DocumentManagementControlledDocumentsPermissions.ControlledDocumentsVersionCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SHARE", "Share", DocumentManagementControlledDocumentsPermissions.ControlledDocumentsShare, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "CONTROLLED_DOCUMENTS_VERSIONS",
                    DisplayName: "Version History",
                    RoutePath: "/DocumentManagementControlledDocuments/VersionHistory/{id}",
                    RequiredPermission: DocumentManagementControlledDocumentsPermissions.ControlledDocumentsVersionView,
                    ParentPageCode: "CONTROLLED_DOCUMENTS_DETAILS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 33,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: "CONTROLLED_DOCUMENTS_SHARE",
                    DisplayName: "Share Controlled Document",
                    RoutePath: "/DocumentManagementControlledDocuments/Share/{id}",
                    RequiredPermission: DocumentManagementControlledDocumentsPermissions.ControlledDocumentsShare,
                    ParentPageCode: "CONTROLLED_DOCUMENTS_DETAILS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 34,
                    Actions:
                    [
                        new ModuleManifestAction("SHARE", "Share", DocumentManagementControlledDocumentsPermissions.ControlledDocumentsShare, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // ── Corporate Template Masters (canonical template register) ──────────────────────────────
                new ModuleManifestPage(
                    PageCode: "TEMPLATE_MASTERS",
                    DisplayName: "Corporate Template Masters",
                    RoutePath: "/DocumentManagementTemplateMasters",
                    RequiredPermission: DocumentManagementTemplateMasterPermissions.View,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "New Template Master", DocumentManagementTemplateMasterPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DEPRECATE", "Deprecate", DocumentManagementTemplateMasterPermissions.Deprecate, "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", DocumentManagementTemplateMasterPermissions.Delete, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: "TEMPLATE_MASTERS_CREATE",
                    DisplayName: "New Template Master",
                    RoutePath: "/DocumentManagementTemplateMasters/Create",
                    RequiredPermission: DocumentManagementTemplateMasterPermissions.Create,
                    ParentPageCode: "TEMPLATE_MASTERS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 41,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Template Master", DocumentManagementTemplateMasterPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "TEMPLATE_MASTERS_DETAILS",
                    DisplayName: "Template Master Details",
                    RoutePath: "/DocumentManagementTemplateMasters/Details/{id}",
                    RequiredPermission: DocumentManagementTemplateMasterPermissions.View,
                    ParentPageCode: "TEMPLATE_MASTERS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 42,
                    Actions:
                    [
                        new ModuleManifestAction("PUBLISH_VERSION", "Publish Version", DocumentManagementTemplateMasterPermissions.VersionPublish, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_IMPACT", "View Impact", DocumentManagementTemplateMasterPermissions.ImpactView, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DEPRECATE", "Deprecate", DocumentManagementTemplateMasterPermissions.Deprecate, "Toolbar", 30, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DELETE", "Delete", DocumentManagementTemplateMasterPermissions.Delete, "Toolbar", 40, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // ── Template Variants (master-derived, folder-scoped variants) ────────────────────────────
                new ModuleManifestPage(
                    PageCode: "TEMPLATE_VARIANTS",
                    DisplayName: "Template Variants",
                    RoutePath: "/DocumentManagementTemplateVariants",
                    RequiredPermission: DocumentManagementTemplateVariantPermissions.View,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "New Template Variant", DocumentManagementTemplateVariantPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("COMPARE", "Compare", DocumentManagementTemplateVariantPermissions.Compare, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REBASE", "Rebase to Master", DocumentManagementTemplateVariantPermissions.Rebase, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: "TEMPLATE_VARIANTS_CREATE",
                    DisplayName: "New Template Variant",
                    RoutePath: "/DocumentManagementTemplateVariants/Create",
                    RequiredPermission: DocumentManagementTemplateVariantPermissions.Create,
                    ParentPageCode: "TEMPLATE_VARIANTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 51,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Template Variant", DocumentManagementTemplateVariantPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "TEMPLATE_VARIANTS_DETAILS",
                    DisplayName: "Template Variant Details",
                    RoutePath: "/DocumentManagementTemplateVariants/Details/{id}",
                    RequiredPermission: DocumentManagementTemplateVariantPermissions.View,
                    ParentPageCode: "TEMPLATE_VARIANTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 52,
                    Actions:
                    [
                        new ModuleManifestAction("COMPARE", "Compare", DocumentManagementTemplateVariantPermissions.Compare, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("REBASE", "Rebase to Master", DocumentManagementTemplateVariantPermissions.Rebase, "Toolbar", 20, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // ── Document Access Matrix (folder/document access policies) ──────────────────────────────
                new ModuleManifestPage(
                    PageCode: "ACCESS_MATRIX",
                    DisplayName: "Document Access Matrix",
                    RoutePath: "/DocumentManagementAccessMatrix",
                    RequiredPermission: DocumentManagementAccessPermissions.View,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 60,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "New Access Policy", DocumentManagementAccessPermissions.Manage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PREVIEW", "Preview Access", DocumentManagementAccessPermissions.Preview, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "ACCESS_MATRIX_CREATE",
                    DisplayName: "New Access Policy",
                    RoutePath: "/DocumentManagementAccessMatrix/Create",
                    RequiredPermission: DocumentManagementAccessPermissions.Manage,
                    ParentPageCode: "ACCESS_MATRIX",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 61,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Policy", DocumentManagementAccessPermissions.Manage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "ACCESS_MATRIX_DETAILS",
                    DisplayName: "Access Policy Details",
                    RoutePath: "/DocumentManagementAccessMatrix/Details/{id}",
                    RequiredPermission: DocumentManagementAccessPermissions.View,
                    ParentPageCode: "ACCESS_MATRIX",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 62,
                    Actions:
                    [
                        new ModuleManifestAction("PREVIEW", "Preview Access", DocumentManagementAccessPermissions.Preview, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "ACCESS_MATRIX_EDIT",
                    DisplayName: "Edit Access Policy",
                    RoutePath: "/DocumentManagementAccessMatrix/Edit/{id}",
                    RequiredPermission: DocumentManagementAccessPermissions.Manage,
                    ParentPageCode: "ACCESS_MATRIX_DETAILS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 63,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Policy", DocumentManagementAccessPermissions.Manage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // MOD-0029-FU24 Document Master Register + FU28A Repository Assessments — post main-sync catalogue
                // registration (2026-08-28). The hand-written tenant-shell links these two had were removed with main's
                // MOD-0285 DynamicModuleMenu adoption, so these descriptors are now the only source of their sidebar
                // entry. RequiredPermission is the verbatim key the FU06/FU16 backend [HasPermission] enforces.
                new ModuleManifestPage(
                    PageCode: "MASTER_REGISTER",
                    DisplayName: "Document Master Register",
                    RoutePath: "/DocumentManagementMasterRegister",
                    RequiredPermission: "platform.document-management.master-register.view",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 70,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: "REPOSITORY_ASSESSMENT",
                    DisplayName: "Repository Assessments",
                    RoutePath: "/DocumentManagementRepositoryAssessments",
                    RequiredPermission: "platform.document-management.repository-assessment.view",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 80,
                    Actions: [])
            ]);
}
