using System.Xml.Linq;
using System.Text.RegularExpressions;
using Diten.Web.Navigation;
using Xunit;

namespace Diten.Web.Tests.PPM;

public sealed class PpmPhase2AContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string WebRoot = Path.Combine(RepoRoot, "frontend", "Diten.Web");
    private static readonly string[] Resources = ["portfolios", "initiatives", "programs", "projects"];
    private static readonly string[] Cultures = ["en", "fr", "es", "zh", "ar", "ru", "tr"];

    [Fact]
    public void Manifest_is_complete_unique_and_tenant_route_scoped()
    {
        Assert.Equal("MOD-0117", PpmModuleManifest.ModuleId);
        Assert.Equal("/ppm", PpmModuleManifest.RootRoute);
        Assert.Equal(7, PpmModuleManifest.Items.Count);
        Assert.Equal(7, PpmModuleManifest.Items.Select(x => x.Route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(PpmModuleManifest.Items, item =>
        {
            Assert.Equal("PPM", item.ModuleKey);
            Assert.StartsWith("/ppm", item.Route, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/api/", item.Route, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("5061", item.Route, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Frontend_manifest_permission_catalog_matches_backend_canonical_catalog()
    {
        var contextsPath = Path.Combine(
            RepoRoot,
            "services",
            "Diten.PpmService",
            "src",
            "Diten.PpmService.Application",
            "Common",
            "Contexts.cs");
        var backendSource = File.ReadAllText(contextsPath);
        var backendPermissions = Regex.Matches(
                backendSource,
                "public const string [A-Za-z0-9_]+ = \"(?<permission>ppm\\.[a-z-]+\\.[a-z-]+)\";")
            .Select(match => match.Groups["permission"].Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var frontendPermissions = PpmModuleManifest.Provider.GetPermissions()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(backendPermissions, frontendPermissions);
        Assert.Equal(frontendPermissions.Length, frontendPermissions.Distinct(StringComparer.Ordinal).Count());
        Assert.All(frontendPermissions, permission =>
            Assert.Matches("^ppm\\.[a-z-]+\\.[a-z-]+$", permission));
    }

    [Fact]
    public void All_four_surfaces_use_tenant_shell_and_shared_datatable_v2_contract()
    {
        var sharedTable = Read("Views", "PPM", "Shared", "_DataTable.cshtml");
        Assert.Contains("data-dt-standard=\"v2\"", sharedTable);
        Assert.Contains("skeleton-loader", sharedTable);

        foreach (var resource in Resources)
        {
            var folder = char.ToUpperInvariant(resource[0]) + resource[1..];
            var index = Read("Views", "PPM", folder, "Index.cshtml");
            Assert.Contains("Layout = \"_LayoutTenantShell\"", index);
            Assert.Contains("_ListShell.cshtml", index);
            Assert.DoesNotContain("_Layout.cshtml", index);
        }
    }

    [Fact]
    public void Ppm_javascript_never_calls_service_port_or_exposes_forbidden_bulk_or_approval_actions()
    {
        var jsRoot = Path.Combine(WebRoot, "wwwroot", "assets", "js", "PPM");
        var source = string.Join('\n', Directory.EnumerateFiles(jsRoot, "*.js", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.DoesNotContain("5061", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bulk approve", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bulk delete", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approve", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ppm_proxy_forwards_the_signed_token_but_never_asserts_tenant_identity_with_a_header()
    {
        var controller = Read("Controllers", "PPM", "PpmController.cs");

        Assert.Contains("AuthenticationHeaderValue(\"Bearer\", token)", controller);
        Assert.DoesNotContain("X-Tenant-Id", controller, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[ValidateAntiForgeryToken]", controller);
    }

    [Fact]
    public void Shared_navigation_keys_have_seven_language_parity_and_nonempty_values()
    {
        var required = new[]
        {
            "Nav.Module.PPM", "Nav.Page.PPM", "Nav.Page.PORTFOLIOS",
            "Nav.Page.INITIATIVES", "Nav.Page.PROGRAMS", "Nav.Page.PROJECTS",
            "Nav.Page.INVESTMENTCASES", "Nav.Page.BENEFITCOMMITMENTS"
        };

        foreach (var culture in Cultures)
        {
            var path = Path.Combine(WebRoot, "Resources", $"SharedResource.{culture}.resx");
            var values = XDocument.Load(path).Descendants("data")
                .ToDictionary(x => (string)x.Attribute("name")!, x => x.Element("value")?.Value);
            foreach (var key in required)
                Assert.False(string.IsNullOrWhiteSpace(values.GetValueOrDefault(key)), $"{culture}: {key}");
        }
    }

    [Fact]
    public void Each_surface_has_complete_seven_language_resource_set()
    {
        foreach (var resource in Resources)
        {
            var folder = char.ToUpperInvariant(resource[0]) + resource[1..];
            var marker = $"{folder}Index";
            foreach (var culture in Cultures)
            {
                var path = Path.Combine(WebRoot, "Resources", "Views", "PPM", folder, $"{marker}.{culture}.resx");
                Assert.True(File.Exists(path), path);
                Assert.DoesNotContain(
                    XDocument.Load(path).Descendants("data"),
                    item => string.IsNullOrWhiteSpace(item.Element("value")?.Value));
            }
        }
    }

    [Fact]
    public void Project_workspace_is_phase2a_only_tenant_scoped_and_reachable_from_list()
    {
        var controller = Read("Controllers", "PPM", "PpmController.cs");
        var index = Read("Views", "PPM", "Projects", "Index.cshtml");
        var workspace = Read("Views", "PPM", "Projects", "Workspace.cshtml");
        var workspaceJs = Read("wwwroot", "assets", "js", "PPM", "Projects", "workspace.js");
        var crudJs = Read("wwwroot", "assets", "js", "PPM", "ppm-crud.js");

        Assert.Contains("[HttpGet(\"projects/{id:guid}\")]", controller);
        Assert.Contains("HasProjectWorkspace = true", index);
        Assert.Contains("Layout = \"_LayoutTenantShell\"", workspace);
        Assert.Contains("/ppm/projects/api/", workspaceJs);
        Assert.Contains("/ppm/${parentResource}/api/", workspaceJs);
        Assert.Contains("`${parentCode} — ${parentName}`", workspaceJs);
        Assert.DoesNotContain("workspace-parent-id", workspace);
        Assert.Contains("js-open-workspace", crudJs);
        Assert.DoesNotContain("5061", workspaceJs);

        var forbidden = new[]
        {
            "Create Task", "WBS", "Approve", "burn-rate", "capacity",
            "deliverables", "RAID", "recent activity", "SubProject"
        };
        Assert.All(forbidden, value =>
            Assert.DoesNotContain(value, workspace, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Project_workspace_resources_have_seven_language_key_parity()
    {
        string[]? baseline = null;
        foreach (var culture in Cultures)
        {
            var path = Path.Combine(
                WebRoot, "Resources", "Views", "PPM", "Projects", $"ProjectWorkspace.{culture}.resx");
            Assert.True(File.Exists(path), path);
            var keys = XDocument.Load(path).Descendants("data")
                .Select(x => (string)x.Attribute("name")!)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            Assert.DoesNotContain(
                XDocument.Load(path).Descendants("data"),
                item => string.IsNullOrWhiteSpace(item.Element("value")?.Value));
            baseline ??= keys;
            Assert.Equal(baseline, keys);
        }
    }

    [Fact]
    public void Project_workspace_l10n_bridge_and_javascript_share_camel_case_contract()
    {
        var bridge = Read("Views", "PPM", "Projects", "Workspace.cshtml");
        var javascript = Read("wwwroot", "assets", "js", "PPM", "Projects", "workspace.js");

        string[] bridgeKeys =
        [
            "Referenceable", "NotReferenceable", "NotAvailable",
            "Error400", "Error401", "Error403", "Error404", "Error409", "Error503", "ErrorGeneric"
        ];
        string[] javascriptKeys =
        [
            "referenceable", "notReferenceable", "notAvailable",
            "error400", "error401", "error403", "error404", "error409", "error503", "errorGeneric"
        ];

        Assert.All(bridgeKeys, key => Assert.Contains($"{key} = Localizer[", bridge));
        Assert.All(javascriptKeys, key =>
            Assert.Contains(key.StartsWith("error", StringComparison.Ordinal)
                ? $"L[`error${{status}}`] || L.errorGeneric"
                : $"L.{key}", javascript));
        Assert.Contains("value ?? L.notAvailable", javascript);
        Assert.Contains("referenceable ? L.referenceable : L.notReferenceable", javascript);
        Assert.DoesNotContain("L.Referenceable", javascript);
        Assert.DoesNotContain("L.NotReferenceable", javascript);
        Assert.DoesNotContain("L.NotAvailable", javascript);
        Assert.DoesNotContain("L.Error", javascript);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([WebRoot, .. segments]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
