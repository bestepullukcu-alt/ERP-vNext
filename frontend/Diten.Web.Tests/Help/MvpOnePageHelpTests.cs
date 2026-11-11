using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Services.Help;
using Diten.Web.Tests.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Help;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (UXH-001 "?" panel) — every MVP-1 page (MOD-0173 stock ledger, MOD-0174 traceability, MOD-0175 quarantine, MOD-0176
/// expiry / FEFO, MOD-0177 recall, MOD-0022 e-signature — 46 codes, all on the tenant shell) carries its help in the panel's ONE resource
/// (<c>PageHelpResource</c>, 7 languages): Purpose, Audience, 1–5 steps each with its title, no hole; one catalog icon per step; the view
/// names its own code (a child route never shows its parent's help); and the module resources no longer hold a second copy (the old
/// <c>Help.RECALL*</c> / <c>Help.EXPIRY*</c> keys and the <c>ESign.Help.*</c> in-page notes moved here). Measured on the real resx files and
/// the real shell + panel render (LayoutRenderer).
/// </summary>
public sealed class MvpOnePageHelpTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>code → the view that draws it (relative to Views/).</summary>
    public static readonly TheoryData<string, string> Pages = new()
    {
        { "STOCKLEDGER", "Inventory/StockLedger/Index.cshtml" }, { "STOCKLEDGERCREATE", "Inventory/StockLedger/Create.cshtml" },
        { "STOCKMOVEMENTS", "Inventory/StockMovements/Index.cshtml" }, { "STOCKMOVEMENTCREATE", "Inventory/StockMovements/Create.cshtml" },
        { "STOCKMOVEMENTEDIT", "Inventory/StockMovements/Edit.cshtml" }, { "STOCKMOVEMENTDETAILS", "Inventory/StockMovements/Details.cshtml" },
        { "LOCATIONTREE", "Inventory/LocationTree/Index.cshtml" }, { "LOCATIONS", "Inventory/Locations/Index.cshtml" },
        { "LOTS", "Traceability/Lots/Index.cshtml" }, { "LOTDETAILS", "Traceability/Lots/Details.cshtml" }, { "LOTCREATE", "Traceability/Lots/Create.cshtml" },
        { "LOTEDIT", "Traceability/Lots/Edit.cshtml" }, { "LOTMOVEMENTS", "Traceability/LotMovements/Index.cshtml" }, { "SERIALS", "Traceability/Serials/Index.cshtml" },
        { "LOTTRACE", "Traceability/LotTrace/Index.cshtml" }, { "LOTMIGRATION", "Traceability/LotMigration/Index.cshtml" },
        { "QUARANTINEQUEUE", "Quarantine/Index.cshtml" }, { "QUARANTINEDETAILS", "Quarantine/Details.cshtml" }, { "QUARANTINEDECIDE", "Quarantine/Decide.cshtml" },
        { "DECISIONSUBMIT", "Quarantine/Submit.cshtml" }, { "BLOCKEDSTOCK", "Quarantine/Blocked.cshtml" }, { "BLOCKEDDISPOSITION", "Quarantine/Disposition.cshtml" },
        { "RETESTQUEUE", "Quarantine/Retest.cshtml" }, { "QUARANTINESETTINGS", "Quarantine/Settings.cshtml" },
        { "EXPIRYDASHBOARD", "Expiry/Index.cshtml" }, { "EXPIRYAPPROACHING", "Expiry/Approaching.cshtml" }, { "EXPIREDSTOCK", "Expiry/Expired.cshtml" },
        { "FEFOORDER", "Expiry/FefoOrder.cshtml" }, { "EXPIRYEXCEPTIONS", "Expiry/Exceptions.cshtml" }, { "EXPIRYEXCEPTIONDETAILS", "Expiry/ExceptionDetails.cshtml" },
        { "EXPIRYPOLICY", "Expiry/Policy.cshtml" }, { "EXPIRYEVIDENCE", "Expiry/Evidence.cshtml" },
        { "RECALLDASHBOARD", "Recall/Index.cshtml" }, { "RECALLTRACE", "Recall/Trace.cshtml" }, { "RECALLCASES", "Recall/Cases.cshtml" },
        { "RECALLCASEDETAILS", "Recall/CaseDetails.cshtml" }, { "RECALLDRILLS", "Recall/Drills.cshtml" }, { "RECALLEVIDENCE", "Recall/Evidence.cshtml" },
        { "RECALLSETTINGS", "Recall/Settings.cshtml" },
        { "ESIGNREQUESTS", "ESignature/Requests.cshtml" }, { "ESIGNVERIFICATION", "ESignature/Verification.cshtml" }, { "ESIGNAUDITEXPORT", "ESignature/AuditExport.cshtml" },
        { "ESIGNMYSIGNATURE", "ESignature/MySignature.cshtml" }, { "ESIGNPOLICY", "ESignature/Settings/Policy.cshtml" },
        { "ESIGNMEANINGS", "ESignature/Settings/Meanings.cshtml" }, { "ESIGNSAMPLEAPPROVALS", "ESignature/Settings/SampleApprovals.cshtml" }
    };

    private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Help = new(() => Languages.ToDictionary(l => l, l =>
        XDocument.Load(Path.Combine(Web, "Resources", $"PageHelpResource.{l}.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal)));

    private static int Steps(string code) => Enumerable.Range(1, 5).TakeWhile(n => Help.Value["en"].ContainsKey($"Help.{code}.Step{n}")).Count();

    [Theory]
    [MemberData(nameof(Pages))]
    public void The_page_has_purpose_audience_and_titled_steps_in_all_seven_languages(string code, string view)
    {
        var steps = Steps(code);
        Assert.InRange(steps, 1, 5);
        foreach (var lang in Languages)
        {
            var text = Help.Value[lang];
            var parts = new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, steps).SelectMany(n => new[] { $"Step{n}", $"Step{n}Title" }));
            foreach (var part in parts)
                Assert.False(string.IsNullOrWhiteSpace(text.GetValueOrDefault($"Help.{code}.{part}")), $"{lang}: Help.{code}.{part} is missing ({view})");
            Assert.False(text.ContainsKey($"Help.{code}.Step{steps + 1}"), $"{lang}: Help.{code} has a hole after step {steps}");
            Assert.All(text.Keys.Where(k => k.StartsWith($"Help.{code}.", StringComparison.Ordinal)),
                k => Assert.Matches($@"^Help\.{code}\.(Purpose|Audience|Immediate|Step[1-5](Title)?)$", k));
            Assert.DoesNotMatch("<[a-zA-Z/]", text[$"Help.{code}.Purpose"]);                 // text, never HTML
        }

        Assert.Equal(Help.Value["en"].ContainsKey($"Help.{code}.Immediate"), Languages.All(l => Help.Value[l].ContainsKey($"Help.{code}.Immediate")));
        Assert.NotEqual(Help.Value["en"][$"Help.{code}.Purpose"], Help.Value["tr"][$"Help.{code}.Purpose"]);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public void Every_step_has_its_catalog_icon_and_the_view_names_its_own_code(string code, string view)
    {
        var steps = Steps(code);
        for (var n = 1; n <= steps; n++) Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, n));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, steps + 1));     // one icon per step, no more

        var source = File.ReadAllText(Path.Combine([Web, "Views", .. view.Split('/')]));
        Assert.Contains($"ViewData[\"PageCode\"] = \"{code}\";", source, StringComparison.Ordinal);
        Assert.Contains("Layout = \"_LayoutTenantShell\";", source, StringComparison.Ordinal);
        Assert.DoesNotContain("esignPageHelp", source, StringComparison.Ordinal);                        // the in-page note moved to the panel
        Assert.DoesNotContain("ESign.Help.", source, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task The_shell_panel_draws_the_pages_purpose_and_its_step_cards(string code, string view)
    {
        var page = await LayoutRenderer.RenderAsync(factory, "/Views/MasterDataManagement/Lskus/Index.cshtml", "Lskus", "mdm.lskus.read",
            new Dictionary<string, object?> { ["PageCode"] = code });
        var start = page.IndexOf("id=\"diten-help-panel\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{code}: no help panel ({view})");
        var panel = page[start..];
        Assert.Contains($"data-page-code=\"{code}\"", page, StringComparison.Ordinal);
        Assert.Contains("data-diten-help-purpose", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("data-diten-help-preparing", panel, StringComparison.Ordinal);
        Assert.Equal(Steps(code), Regex.Matches(panel, "data-diten-help-step=\"").Count);
        Assert.Equal(3, Regex.Matches(panel, "role=\"tab\"").Count);
    }

    [Fact]
    public void The_module_resources_hold_no_second_copy_of_the_page_help()
    {
        Assert.DoesNotContain(Diten.Web.Views.Recall.Recall.Keys, k => k.StartsWith("Help.", StringComparison.Ordinal));
        Assert.DoesNotContain(Diten.Web.Views.Expiry.Expiry.Keys, k => k.StartsWith("Help.", StringComparison.Ordinal));
        Assert.DoesNotContain(Diten.Web.Views.ESignature.ESignature.Keys, k => k.StartsWith("ESign.Help.", StringComparison.Ordinal));
        foreach (var (folder, pattern) in new[] { ("Recall", @"^Help\."), ("Expiry", @"^Help\."), ("ESignature", @"^ESign\.Help\.") })
            foreach (var lang in Languages)
            {
                var names = XDocument.Load(Path.Combine(Web, "Resources", "Views", folder, $"{folder}.{lang}.resx")).Root!.Elements("data")
                    .Select(d => (string)d.Attribute("name")!);
                Assert.DoesNotContain(names, n => Regex.IsMatch(n, pattern));
            }

        // the one modal help that stays inside its modal (WP-MVP1-FU-CLEAN1 g, "Stoğu karantinaya al"): not a page, not in the panel resource
        Assert.Contains("Help.QUARANTINEMANUALHOLD.Purpose", Diten.Web.Views.Quarantine.Quarantine.Keys);
        Assert.DoesNotContain(Help.Value["en"].Keys, k => k.StartsWith("Help.QUARANTINEMANUALHOLD.", StringComparison.Ordinal));
    }
}
