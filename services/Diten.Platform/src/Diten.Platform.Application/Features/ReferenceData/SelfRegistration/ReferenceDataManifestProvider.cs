using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.ReferenceData.SelfRegistration;

/// <summary>
/// MC-3b-expand — the Business Reference Data (MasterDataManagement) module self-registration manifest. Mirrors the
/// REAL frontend <c>ReferenceDataController</c> view-routes (sets list, set/version detail, draft wizard, hierarchy /
/// attributes / mappings managers, publish review, usage CRUD, import preview) and the verbatim
/// <c>Platform.BusinessReferenceData.*</c> keys the backend enforces (<c>BusinessReferenceDataController</c>).
/// Permissions are raw strings (this controller gates by literal, not a *Permissions constant class); the
/// completeness test reflects the real enforced keys off the API controller so the mirror stays zero-drift.
/// EVERY route is gated by Read (the controller's EnsureReadAccess), so each page's RequiredPermission is Read;
/// the stronger create/version/import/usage keys live on the action buttons. Sets is the single top-level nav
/// entry; every other route is a sub-page reached from a parent (nav=false). SOFT fields are operator-owned.
/// </summary>
public sealed class ReferenceDataManifestProvider : IModuleManifestProvider
{
    // Verbatim [HasPermission] keys (raw strings — this controller does not use a constant class).
    private const string Read = "Platform.BusinessReferenceData.Read";
    private const string SetCreate = "Platform.BusinessReferenceData.Create";
    private const string SetUpdate = "Platform.BusinessReferenceData.Update";
    private const string VersionCreate = "Platform.BusinessReferenceData.Version.Create";
    private const string VersionUpdate = "Platform.BusinessReferenceData.Version.Update";
    private const string VersionValidate = "Platform.BusinessReferenceData.Version.Validate";
    private const string VersionSubmit = "Platform.BusinessReferenceData.Version.Submit";
    private const string VersionApprove = "Platform.BusinessReferenceData.Version.Approve";
    private const string VersionPublish = "Platform.BusinessReferenceData.Version.Publish";
    private const string VersionPublishOverride = "Platform.BusinessReferenceData.Version.PublishOverride";
    private const string ImportPreview = "Platform.BusinessReferenceData.Import.Preview";
    private const string ImportCommit = "Platform.BusinessReferenceData.Import.Commit";
    private const string UsageRegister = "Platform.BusinessReferenceData.Usage.Register";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "reference-data",
            ModuleName: "ReferenceData",
            DisplayName: "Reference Data",
            Domain: "MasterDataManagement",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 320,
            Pages:
            [
                // Sets list — the navigable entry. Toolbar Add-Set / Import; the row Open / Hierarchy / Attributes /
                // Mappings / Usage links NAVIGATE to their own routes (pages below), so they are not actions.
                new ModuleManifestPage(
                    PageCode: "RD_SETS",
                    DisplayName: "Reference Data Sets",
                    RoutePath: "/Platform/ReferenceData",
                    RequiredPermission: Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_SET", "Add Set", SetCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("IMPORT", "Import", ImportPreview, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_SET_DETAIL",
                    DisplayName: "Set Detail",
                    RoutePath: "/Platform/ReferenceData/Sets/{setId}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SETS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_DRAFT", "Create Draft", VersionCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("UPDATE_SET", "Save Set", SetUpdate, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("IMPORT_TO_DRAFT", "Import to Draft", ImportPreview, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VALIDATE", "Validate Draft", VersionValidate, "Toolbar", 40, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SUBMIT", "Submit for Approval", VersionSubmit, "Toolbar", 50, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_DRAFT_WIZARD",
                    DisplayName: "Draft Wizard",
                    RoutePath: "/Platform/ReferenceData/Sets/{setId}/DraftWizard",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SET_DETAIL",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_DRAFT", "Start Draft", VersionCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SAVE_VALUES", "Save Draft Values", VersionUpdate, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("IMPORT_TO_DRAFT", "Import to Draft", ImportPreview, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VALIDATE", "Run Validation", VersionValidate, "Toolbar", 40, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SUBMIT", "Submit for Approval", VersionSubmit, "Toolbar", 50, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_VERSION_DETAIL",
                    DisplayName: "Version Detail",
                    RoutePath: "/Platform/ReferenceData/Versions/{versionId}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SET_DETAIL",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE_VALUES", "Save Values", VersionUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VALIDATE", "Validate", VersionValidate, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SUBMIT", "Submit for Approval", VersionSubmit, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_HIERARCHY",
                    DisplayName: "Hierarchy Manager",
                    RoutePath: "/Platform/ReferenceData/Hierarchy/{setCode}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SETS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 14,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE_HIERARCHY", "Save Hierarchy", VersionUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_ATTRIBUTES",
                    DisplayName: "Attribute Manager",
                    RoutePath: "/Platform/ReferenceData/Attributes/{setCode}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SETS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 15,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE_ATTRIBUTES", "Save Attributes", VersionUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_MAPPINGS",
                    DisplayName: "Mapping Manager",
                    RoutePath: "/Platform/ReferenceData/Mappings/{setCode}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SETS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 16,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE_MAPPING", "Save Mapping", VersionUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_PUBLISH_REVIEW",
                    DisplayName: "Publish Review",
                    RoutePath: "/Platform/ReferenceData/PublishReview/{versionId}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_VERSION_DETAIL",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 17,
                    Actions:
                    [
                        new ModuleManifestAction("VALIDATE", "Validate", VersionValidate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SUBMIT", "Submit for Approval", VersionSubmit, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("APPROVE", "Approve", VersionApprove, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PUBLISH", "Publish", VersionPublish, "Toolbar", 40, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PUBLISH_OVERRIDE", "Publish (Override)", VersionPublishOverride, "Toolbar", 50, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_USAGE",
                    DisplayName: "Usage Dependencies",
                    RoutePath: "/Platform/ReferenceData/Usage/{setCode}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SETS",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 18,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_USAGE", "Add Usage", UsageRegister, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DELETE_USAGE", "Delete Usage", UsageRegister, "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_USAGE_CREATE",
                    DisplayName: "Create Usage Registration",
                    RoutePath: "/Platform/ReferenceData/Usage/{setCode}/Create",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_USAGE",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 19,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Usage", UsageRegister, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "RD_USAGE_EDIT",
                    DisplayName: "Edit Usage Registration",
                    RoutePath: "/Platform/ReferenceData/Usage/{setCode}/Edit/{usageRegistrationId}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_USAGE",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Usage", UsageRegister, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                // Read-only detail — the Edit Record button NAVIGATES to the Edit route (a page), so no action.
                new ModuleManifestPage(
                    PageCode: "RD_USAGE_DETAILS",
                    DisplayName: "Usage Registration Details",
                    RoutePath: "/Platform/ReferenceData/Usage/{setCode}/Details/{usageRegistrationId}",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_USAGE",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 21,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: "RD_IMPORT_PREVIEW",
                    DisplayName: "Import Preview",
                    RoutePath: "/Platform/ReferenceData/ImportPreview",
                    RequiredPermission: Read,
                    ParentPageCode: "RD_SETS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 22,
                    Actions:
                    [
                        new ModuleManifestAction("PREVIEW", "Import Preview", ImportPreview, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("COMMIT", "Commit Import", ImportCommit, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ]);
}
