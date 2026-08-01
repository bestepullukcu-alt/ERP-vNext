using System.Xml.Linq;
using Diten.Web.Navigation;
using Xunit;

namespace Diten.Web.Tests.PPM;

public sealed class PpmGateLFrontendContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string WebRoot = Path.Combine(RepoRoot, "frontend", "Diten.Web");
    private static readonly string[] Cultures = ["en", "fr", "es", "zh", "ar", "ru", "tr"];

    [Fact]
    public void Gate_l_surfaces_are_tenant_shell_golden_slim_same_origin_routes()
    {
        var controller = Read("Controllers", "PPM", "PpmController.cs");
        Assert.Contains("investment-cases", controller);
        Assert.Contains("benefit-commitments", controller);
        Assert.Contains("credentials: 'same-origin'", Read("wwwroot", "assets", "js", "PPM", "ppm-crud.js"));

        foreach (var folder in new[] { "InvestmentCases", "BenefitCommitments" })
        {
            var index = Read("Views", "PPM", folder, "Index.cshtml");
            Assert.Contains("Layout = \"_LayoutTenantShell\"", index);
            Assert.Contains("_ListShell.cshtml", index);
            Assert.DoesNotContain("_Layout.cshtml", index);
            Assert.True(File.Exists(Path.Combine(WebRoot, "Views", "PPM", folder, "_CreateEditOffcanvas.cshtml")));
        }
    }

    [Fact]
    public void Investment_case_and_benefit_contracts_are_closed_and_do_not_expose_forbidden_ownership()
    {
        var investment = Read("wwwroot", "assets", "js", "PPM", "InvestmentCases", "index.js");
        var benefit = Read("wwwroot", "assets", "js", "PPM", "BenefitCommitments", "index.js");
        Assert.Contains("titleProperty: 'title'", investment);
        Assert.Contains("hasPortfolio: true", investment);
        Assert.Contains("hasPlanningDates: true", investment);
        Assert.Contains("Draft: ['UnderAnalysis', 'Withdrawn']", investment);
        Assert.Contains("UnderAnalysis: ['Closed', 'Withdrawn']", investment);
        Assert.Contains("immutableParent: true", investment);
        Assert.Contains("hasInvestmentCaseParent: true", benefit);
        Assert.Contains("hasBenefitTarget: true", benefit);
        Assert.Contains("Active: ['Closed', 'Cancelled']", benefit);
        Assert.Contains("showReferenceability: false", benefit);
        Assert.DoesNotContain("PortfolioId", Read("Views", "PPM", "BenefitCommitments", "Index.cshtml"));
    }

    [Fact]
    public void Navigation_permissions_and_localization_cover_gate_l()
    {
        Assert.Contains(PpmModuleManifest.Items, x => x.Route == "/ppm/investment-cases");
        Assert.Contains(PpmModuleManifest.Items, x => x.Route == "/ppm/benefit-commitments");
        Assert.Equal(8, PpmModuleManifest.Permissions.Count(x =>
            x.StartsWith("ppm.investment-cases.") || x.StartsWith("ppm.benefit-commitments.")));

        foreach (var culture in Cultures)
        {
            AssertResource("InvestmentCases", "InvestmentCasesIndex", culture);
            AssertResource("BenefitCommitments", "BenefitCommitmentsIndex", culture);
            var shared = Resource(Path.Combine(WebRoot, "Resources", $"SharedResource.{culture}.resx"));
            Assert.False(string.IsNullOrWhiteSpace(shared["Nav.Page.INVESTMENTCASES"]));
            Assert.False(string.IsNullOrWhiteSpace(shared["Nav.Page.BENEFITCOMMITMENTS"]));
        }
    }

    [Fact]
    public void Shared_crud_uses_sweetalert_and_code_title_parent_display_without_service_ports()
    {
        var crud = Read("wwwroot", "assets", "js", "PPM", "ppm-crud.js");
        Assert.Contains("window.showConfirm", crud);
        Assert.Contains("`${row.code} — ${titleOf(row)}`", crud);
        Assert.Contains("`${parent.code} — ${titleOf(parent)}`", crud);
        Assert.DoesNotContain("5061", crud);
        Assert.DoesNotContain("approve", crud, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gate_l_parent_labels_use_the_shared_contract_and_never_opt_into_raw_guid_fallback()
    {
        var investmentView = Read("Views", "PPM", "InvestmentCases", "Index.cshtml");
        var benefitView = Read("Views", "PPM", "BenefitCommitments", "Index.cshtml");
        var investmentJs = Read("wwwroot", "assets", "js", "PPM", "InvestmentCases", "index.js");
        var crud = Read("wwwroot", "assets", "js", "PPM", "ppm-crud.js");

        Assert.Contains("ViewData[\"ParentText\"] = Localizer[\"Portfolio\"]", investmentView);
        Assert.Contains("ViewData[\"ParentText\"] = Localizer[\"InvestmentCase\"]", benefitView);
        Assert.DoesNotContain("ViewData[\"PortfolioText\"]", investmentView);
        Assert.DoesNotContain("ViewData[\"InvestmentCaseText\"]", benefitView);
        Assert.Contains("hideRawParentId: true", investmentJs);
        Assert.Contains("config.hideRawParentId ? (L.NotAvailable || '-')", crud);
        Assert.Contains("parent ? `${parent.code} — ${titleOf(parent)}` : (L.NotAvailable || '-')", crud);
    }

    private static void AssertResource(string folder, string marker, string culture)
    {
        var path = Path.Combine(WebRoot, "Resources", "Views", "PPM", folder, $"{marker}.{culture}.resx");
        Assert.True(File.Exists(path), path);
        Assert.DoesNotContain(Resource(path), pair => string.IsNullOrWhiteSpace(pair.Value));
    }

    private static Dictionary<string, string> Resource(string path) => XDocument.Load(path).Descendants("data")
        .ToDictionary(x => (string)x.Attribute("name")!, x => x.Element("value")?.Value ?? "");

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([WebRoot, .. parts]));

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
