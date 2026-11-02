using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.PvgService.Api.ModuleRegistration;

/// <summary>
/// MOD-0230 closed catalog registration (WP-0230-DL11; pack "Closed Catalog Registration"). The manifest mirrors the
/// frontend with zero drift: the four view routes of the case intake controller and the buttons its views show. While the
/// operational gate is closed the module is hidden and not assignable to a tenant: no page is visible in the menu, the
/// module is not baseline, and no API-only permission or notification event is declared. The module owns all five keys,
/// so no page or action names another owner. This is a pack-scoped exception to the self-registration standard (the top
/// level page is not menu-visible); it ends when the operational gate opens.
/// </summary>
public sealed class PvgCaseIntakeTriageManifestProvider : IModuleManifestProvider
{
    private const string ReadKey = "pvg.case-intake-triage.read";
    private const string CreateKey = "pvg.case-intake-triage.create";
    private const string UpdateKey = "pvg.case-intake-triage.update";
    private const string TriageKey = "pvg.case-intake-triage.triage";
    private const string RouteKey = "pvg.case-intake-triage.route";

    // One label per key: the permission sync overwrites a key's display name on every push, so the last row wins.
    private const string ReadLabel = "View Case Intake";
    private const string CreateLabel = "Create Case Intake";
    private const string UpdateLabel = "Edit Case Intake";
    private const string TriageLabel = "Triage Case Intake";
    private const string RouteLabel = "Route Case Intake";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "case-intake-triage",
            ModuleName: "Case Intake & Triage",
            DisplayName: "Case Intake & Triage",
            Domain: "Pharmacovigilance",
            Service: "PvgService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: false,
            SortOrder: 230,
            Icon: "bx-shield-quarter",
            IsBaseline: false,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "CASE_INTAKE_TRIAGE",
                    DisplayName: ReadLabel,
                    RoutePath: "/Pharmacovigilance/CaseIntakeTriage",
                    RequiredPermission: ReadKey,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction(
                            ActionCode: "CREATE",
                            DisplayName: CreateLabel,
                            PermissionKey: CreateKey,
                            ActionType: "Toolbar",
                            SortOrder: 10,
                            IsDangerous: false,
                            IsToolbarAction: true,
                            IsRowAction: false),
                        new ModuleManifestAction(
                            ActionCode: "VIEW",
                            DisplayName: ReadLabel,
                            PermissionKey: ReadKey,
                            ActionType: "RowAction",
                            SortOrder: 20,
                            IsDangerous: false,
                            IsToolbarAction: false,
                            IsRowAction: true),
                        new ModuleManifestAction(
                            ActionCode: "EDIT",
                            DisplayName: UpdateLabel,
                            PermissionKey: UpdateKey,
                            ActionType: "RowAction",
                            SortOrder: 30,
                            IsDangerous: false,
                            IsToolbarAction: false,
                            IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "CASE_INTAKE_TRIAGE_CREATE",
                    DisplayName: CreateLabel,
                    RoutePath: "/Pharmacovigilance/CaseIntakeTriage/Create",
                    RequiredPermission: CreateKey,
                    ParentPageCode: "CASE_INTAKE_TRIAGE",
                    IsNavigationVisible: false,
                    PageType: "Create",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction(
                            ActionCode: "SAVE",
                            DisplayName: CreateLabel,
                            PermissionKey: CreateKey,
                            ActionType: "Toolbar",
                            SortOrder: 10,
                            IsDangerous: false,
                            IsToolbarAction: true,
                            IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "CASE_INTAKE_TRIAGE_EDIT",
                    DisplayName: UpdateLabel,
                    RoutePath: "/Pharmacovigilance/CaseIntakeTriage/Edit/{intakeDraftId}",
                    RequiredPermission: UpdateKey,
                    ParentPageCode: "CASE_INTAKE_TRIAGE",
                    IsNavigationVisible: false,
                    PageType: "Edit",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction(
                            ActionCode: "SAVE",
                            DisplayName: UpdateLabel,
                            PermissionKey: UpdateKey,
                            ActionType: "Toolbar",
                            SortOrder: 10,
                            IsDangerous: false,
                            IsToolbarAction: true,
                            IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "CASE_INTAKE_TRIAGE_DETAILS",
                    DisplayName: ReadLabel,
                    RoutePath: "/Pharmacovigilance/CaseIntakeTriage/Details/{intakeDraftId}",
                    RequiredPermission: ReadKey,
                    ParentPageCode: "CASE_INTAKE_TRIAGE",
                    IsNavigationVisible: false,
                    PageType: "Details",
                    SortOrder: 13,
                    Actions:
                    [
                        new ModuleManifestAction(
                            ActionCode: "EDIT",
                            DisplayName: UpdateLabel,
                            PermissionKey: UpdateKey,
                            ActionType: "Toolbar",
                            SortOrder: 10,
                            IsDangerous: false,
                            IsToolbarAction: true,
                            IsRowAction: false),
                        new ModuleManifestAction(
                            ActionCode: "TRIAGE",
                            DisplayName: TriageLabel,
                            PermissionKey: TriageKey,
                            ActionType: "Toolbar",
                            SortOrder: 20,
                            IsDangerous: false,
                            IsToolbarAction: true,
                            IsRowAction: false),
                        new ModuleManifestAction(
                            ActionCode: "ROUTE",
                            DisplayName: RouteLabel,
                            PermissionKey: RouteKey,
                            ActionType: "Toolbar",
                            SortOrder: 30,
                            IsDangerous: false,
                            IsToolbarAction: true,
                            IsRowAction: false)
                    ])
            ]);
}
