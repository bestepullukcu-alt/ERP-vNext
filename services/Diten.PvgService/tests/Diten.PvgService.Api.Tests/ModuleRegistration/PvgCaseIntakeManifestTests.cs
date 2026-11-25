using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.PvgService.Api.ModuleRegistration;
using Diten.PvgService.Api.Tests.Persistence;
using Diten.PvgService.Application.RegPvBase;
using Xunit;

namespace Diten.PvgService.Api.Tests.ModuleRegistration;

// WP-0230-DL11, AC-36 (a)-(e): the manifest matches the code. The controller and the views are read, never changed. The
// tables below are the recorded values of the pack section "Closed Catalog Registration" ("Module", "Labels", "Pages",
// "Actions"). AC-36 (f) is the frontend test NavManifestL10nGuardTests, run against the two navigation text keys.
public sealed class PvgCaseIntakeManifestTests
{
    private const string ReadKey = "pvg.case-intake-triage.read";
    private const string CreateKey = "pvg.case-intake-triage.create";
    private const string UpdateKey = "pvg.case-intake-triage.update";
    private const string TriageKey = "pvg.case-intake-triage.triage";
    private const string RouteKey = "pvg.case-intake-triage.route";

    private static readonly string[] PageTypes = ["List", "Details", "Create", "Edit", "Report", "Wizard", "Dashboard", "Custom"];
    private static readonly string[] ActionTypes = ["View", "Toolbar", "RowAction", "FormAction", "BulkAction", "System", "Custom"];

    private static readonly (string PageCode, string DisplayName, string RoutePath, string Key, string? Parent, string PageType, int SortOrder)[] RecordedPages =
    [
        ("CASE_INTAKE_TRIAGE", "View Case Intake", "/Pharmacovigilance/CaseIntakeTriage", ReadKey, null, "List", 10),
        ("CASE_INTAKE_TRIAGE_CREATE", "Create Case Intake", "/Pharmacovigilance/CaseIntakeTriage/Create", CreateKey, "CASE_INTAKE_TRIAGE", "Create", 11),
        ("CASE_INTAKE_TRIAGE_EDIT", "Edit Case Intake", "/Pharmacovigilance/CaseIntakeTriage/Edit/{intakeDraftId}", UpdateKey, "CASE_INTAKE_TRIAGE", "Edit", 12),
        ("CASE_INTAKE_TRIAGE_DETAILS", "View Case Intake", "/Pharmacovigilance/CaseIntakeTriage/Details/{intakeDraftId}", ReadKey, "CASE_INTAKE_TRIAGE", "Details", 13)
    ];

    private static readonly (string Page, string ActionCode, string DisplayName, string Key, string ActionType, int SortOrder, bool Toolbar, bool Row)[] RecordedActions =
    [
        ("CASE_INTAKE_TRIAGE", "CREATE", "Create Case Intake", CreateKey, "Toolbar", 10, true, false),
        ("CASE_INTAKE_TRIAGE", "VIEW", "View Case Intake", ReadKey, "RowAction", 20, false, true),
        ("CASE_INTAKE_TRIAGE", "EDIT", "Edit Case Intake", UpdateKey, "RowAction", 30, false, true),
        ("CASE_INTAKE_TRIAGE_CREATE", "SAVE", "Create Case Intake", CreateKey, "Toolbar", 10, true, false),
        ("CASE_INTAKE_TRIAGE_EDIT", "SAVE", "Edit Case Intake", UpdateKey, "Toolbar", 10, true, false),
        ("CASE_INTAKE_TRIAGE_DETAILS", "EDIT", "Edit Case Intake", UpdateKey, "Toolbar", 10, true, false),
        ("CASE_INTAKE_TRIAGE_DETAILS", "TRIAGE", "Triage Case Intake", TriageKey, "Toolbar", 20, true, false),
        ("CASE_INTAKE_TRIAGE_DETAILS", "ROUTE", "Route Case Intake", RouteKey, "Toolbar", 30, true, false)
    ];

    private static ModuleManifestDocument Manifest() => new PvgCaseIntakeTriageManifestProvider().GetManifest();

    [Fact]
    public void Case_intake_manifest_is_not_assignable_not_baseline_and_hidden()
    {
        var manifest = Manifest();

        Assert.Equal("case-intake-triage", manifest.ModuleCode);
        Assert.False(manifest.IsTenantAssignable);
        Assert.False(manifest.IsBaseline);
        Assert.Equal(4, manifest.Pages.Count);
        Assert.All(manifest.Pages, page => Assert.False(page.IsNavigationVisible, page.PageCode + " is menu-visible"));
        Assert.True(manifest.ApiPermissions is null || manifest.ApiPermissions.Count == 0, "ApiPermissions must be empty");
    }

    [Fact]
    public void Case_intake_manifest_carries_the_recorded_values()
    {
        var manifest = Manifest();

        Assert.Equal("Case Intake & Triage", manifest.ModuleName);
        Assert.Equal("Case Intake & Triage", manifest.DisplayName);
        Assert.Equal("1.0.0", manifest.ModuleVersion);
        Assert.Equal(230, manifest.SortOrder);
        Assert.Equal("bx-shield-quarter", manifest.Icon);
        Assert.Equal("Pharmacovigilance", manifest.Domain);
        Assert.Equal("PvgService", manifest.Service);
        Assert.True(
            manifest.NotificationEvents is null || manifest.NotificationEvents.Count == 0,
            "NotificationEvents must be empty");

        Assert.Equal(RecordedPages.Length, manifest.Pages.Count);
        foreach (var recorded in RecordedPages)
        {
            var page = Assert.Single(manifest.Pages, candidate => candidate.PageCode == recorded.PageCode);
            Assert.Equal(recorded.DisplayName, page.DisplayName);
            Assert.Equal(recorded.RoutePath, page.RoutePath);
            Assert.Equal(recorded.Key, page.RequiredPermission);
            Assert.Equal(recorded.Parent, page.ParentPageCode);
            Assert.Equal(recorded.PageType, page.PageType);
            Assert.Contains(page.PageType, PageTypes);
            Assert.Equal(recorded.SortOrder, page.SortOrder);
            Assert.Null(page.PermissionOwnerModuleCode);
        }

        var actions = manifest.Pages.SelectMany(page => page.Actions.Select(action => (Page: page.PageCode, Action: action))).ToArray();
        Assert.Equal(RecordedActions.Length, actions.Length);
        foreach (var recorded in RecordedActions)
        {
            var found = Assert.Single(actions, candidate => candidate.Page == recorded.Page && candidate.Action.ActionCode == recorded.ActionCode);
            var action = found.Action;
            Assert.Equal(recorded.DisplayName, action.DisplayName);
            Assert.Equal(recorded.Key, action.PermissionKey);
            Assert.Equal(recorded.ActionType, action.ActionType);
            Assert.Contains(action.ActionType, ActionTypes);
            Assert.Equal(recorded.SortOrder, action.SortOrder);
            Assert.Equal(recorded.Toolbar, action.IsToolbarAction);
            Assert.Equal(recorded.Row, action.IsRowAction);
            Assert.False(action.IsDangerous);
            Assert.Null(action.PermissionOwnerModuleCode);
        }

        // One label per key across pages and actions: the permission sync keeps the last row's label.
        var labels = manifest.Pages
            .Select(page => (Key: page.RequiredPermission, Label: page.DisplayName))
            .Concat(actions.Select(entry => (Key: entry.Action.PermissionKey, Label: entry.Action.DisplayName)))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal);
        foreach (var group in labels)
        {
            Assert.True(
                group.Select(entry => entry.Label).Distinct(StringComparer.Ordinal).Count() == 1,
                group.Key + " carries more than one label");
        }
    }

    [Fact]
    public void Case_intake_manifest_pages_are_exactly_the_four_frontend_view_routes()
    {
        var controllerText = ReadRepositoryFile(ControllerPath);
        var routes = PvgControllerText.ViewRoutes(controllerText).Select(entry => entry.Route).ToArray();

        Assert.Equal(4, routes.Length);

        var manifestRoutes = Manifest().Pages.Select(page => page.RoutePath).ToArray();
        Assert.Empty(routes.Except(manifestRoutes, StringComparer.Ordinal));
        Assert.Empty(manifestRoutes.Except(routes, StringComparer.Ordinal));
    }

    [Fact]
    public void Case_intake_manifest_declares_exactly_the_five_keys_and_each_is_a_real_constant()
    {
        var manifest = Manifest();
        var declared = manifest.Pages.Select(page => page.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(page => page.Actions).Select(action => action.PermissionKey))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(5, declared.Length);

        // The five *Permission constants of the application service (private const, read by reflection).
        var serviceKeys = typeof(PvgIntakeDraftApplicationService)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("Permission", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(5, serviceKeys.Length);
        Assert.Equal(serviceKeys, declared);

        // The five *Permission constants of the MVC controller (read from its text).
        var controllerKeys = PvgControllerText.PermissionConstants(ReadRepositoryFile(ControllerPath)).Values
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(5, controllerKeys.Length);
        Assert.Equal(controllerKeys, declared);
    }

    [Fact]
    public void Case_intake_manifest_page_permissions_match_the_controller_gates()
    {
        var controllerText = ReadRepositoryFile(ControllerPath);
        var constants = PvgControllerText.PermissionConstants(controllerText);
        var gates = PvgControllerText.ViewRoutes(controllerText);
        Assert.Equal(4, gates.Count);

        foreach (var page in Manifest().Pages)
        {
            var gate = Assert.Single(gates, candidate => candidate.Route == page.RoutePath);
            Assert.NotNull(gate.GateConstant);
            Assert.Equal(constants[gate.GateConstant!], page.RequiredPermission);
        }
    }

    [Fact]
    public void Case_intake_manifest_actions_are_the_buttons_the_ui_shows()
    {
        var manifest = Manifest();
        var manifestPairs = manifest.Pages
            .SelectMany(page => page.Actions.Select(action => (Page: page.PageCode, Action: action.ActionCode)))
            .Order()
            .ToArray();

        // The real files, in both directions.
        var extracted = PvgUiButtonExtractor.Extract(
            ReadRepositoryFile(ViewsPath + "Index.cshtml"),
            ReadRepositoryFile(ScriptPath + "index.js"),
            ReadRepositoryFile(ViewsPath + "_Form.cshtml"),
            ReadRepositoryFile(ViewsPath + "Details.cshtml"));
        Assert.NotEmpty(extracted);
        Assert.Empty(extracted.Except(manifestPairs));
        Assert.Empty(manifestPairs.Except(extracted));

        // The check itself: a fixture that carries one more button yields exactly that extra pair.
        var fixture = PvgUiButtonExtractor.Extract(
            "<a href=\"/Pharmacovigilance/CaseIntakeTriage/Create\" class=\"btn\">Create</a>",
            "return window.DitenDataTable.renderActions([ { key: 'quickView', attrs: {} }, { key: 'edit', attrs: {} } ]);",
            "<button type=\"submit\" form=\"pvg-case-intake-form\" class=\"btn\">Save</button>",
            """
            <a href="/Pharmacovigilance/CaseIntakeTriage/Edit/x" class="btn">Edit</a>
            <form id="pvg-triage-form" data-pvg-command-form></form>
            <form id="pvg-route-form" data-pvg-command-form></form>
            <form id="pvg-archive-form" data-pvg-command-form></form>
            """);
        Assert.Equal(
            new[] { (PvgUiButtonExtractor.DetailsPage, "ARCHIVE") },
            fixture.Except(manifestPairs).ToArray());
        Assert.Empty(manifestPairs.Except(fixture));

        // Unique page codes, unique routes, unique action codes per page.
        Assert.Equal(manifest.Pages.Count, manifest.Pages.Select(page => page.PageCode).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(manifest.Pages.Count, manifest.Pages.Select(page => page.RoutePath).Distinct(StringComparer.Ordinal).Count());
        foreach (var page in manifest.Pages)
        {
            Assert.Equal(page.Actions.Count, page.Actions.Select(action => action.ActionCode).Distinct(StringComparer.Ordinal).Count());
        }
    }

    private const string ControllerPath = "frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs";
    private const string ViewsPath = "frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/";
    private const string ScriptPath = "frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/";

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(PvgRepositoryRoot.Find(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
