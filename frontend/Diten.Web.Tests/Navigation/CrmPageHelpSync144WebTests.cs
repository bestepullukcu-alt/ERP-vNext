using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Services.Help;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// WP-SYNC-144 W-UXH (UXH-001, BL-641 / BL-642) — the seven CRM tenant pages of W3 / W4 / W3E carry their "?" help text in
/// the seven tenant languages and leave the ledger; the CRM side panels and dialogs of the same packages mark their primary
/// action (enter / chord) and leave the Enter guard's exception list. Measured on the REAL resx, views, scripts and the two
/// vitest guards' own lists (vitest does not start on this machine — Node 16 — so this suite holds the same facts here;
/// CI's vitest run stays the referee).
/// </summary>
public sealed class CrmPageHelpSync144WebTests
{
    private static readonly string[] Pages =
    [
        "COMPETITORPRODUCTS", "CRMDOCTORINSIGHTTHRESHOLDS", "OBJECTIONRESPONSES", "VISITEVIDENCESETTINGS", "VISITWORKSPACE",
        "CRMVISITQRCODES", "CRMVISITEVIDENCEFLAGS",
        "CRMVISITNOTIFICATIONS" // WP-VW-W5 W5-WEB — a new page arrives with its text (never a ledger row)
    ];

    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>The screens whose words the help text quotes (the page's own resx, plus the pages it names).</summary>
    private static readonly Dictionary<string, string[]> LabelSources = new()
    {
        ["COMPETITORPRODUCTS"] = ["MasterData/CompetitorProducts/CompetitorProductsIndex"],
        ["CRMDOCTORINSIGHTTHRESHOLDS"] = ["CRM/PotentialThresholds/PotentialThresholdsIndex"],
        ["OBJECTIONRESPONSES"] = ["CRM/ObjectionResponses/ObjectionResponsesIndex"],
        ["VISITEVIDENCESETTINGS"] = ["CRM/VisitEvidenceSettings/VisitEvidenceSettingsIndex"],
        ["VISITWORKSPACE"] = ["CRM/VisitWorkspace/VisitWorkspaceIndex"],
        ["CRMVISITQRCODES"] = ["CRM/VisitQrCodes/VisitQrCodesIndex", "CRM/VisitQrCodes/VisitQrIndex", "CRM/VisitEvidenceSettings/VisitEvidenceSettingsIndex"],
        ["CRMVISITEVIDENCEFLAGS"] = ["CRM/VisitEvidenceFlags/VisitEvidenceFlagsIndex"],
        ["CRMVISITNOTIFICATIONS"] = ["CRM/VisitNotifications/VisitNotificationsIndex"]
    };

    // the panel catalog's own quote rule (PageHelpCatalog.Quoted): “…” (en, tr, zh) or «…» / « … » (fr, es, ar, ru)
    private static readonly Regex Quoted = new("“([^”]{1,80})”|«\\u00A0?\\s?([^»]{1,80}?)\\s?\\u00A0?»");

    [Fact]
    public void U1_every_page_has_purpose_audience_and_one_to_five_titled_steps_in_seven_languages()
    {
        var english = Resx("PageHelpResource.en");
        foreach (var language in Languages)
        {
            var resx = Resx("PageHelpResource." + language);
            foreach (var page in Pages)
            {
                var prefix = "Help." + page + ".";
                Assert.True(Has(resx, prefix + "Purpose"), $"{language} {page}.Purpose");
                Assert.True(Has(resx, prefix + "Audience"), $"{language} {page}.Audience");
                var steps = Enumerable.Range(1, 6).TakeWhile(n => resx.ContainsKey(prefix + "Step" + n)).Count();
                Assert.InRange(steps, 1, 5);
                for (var n = 1; n <= steps; n++)
                {
                    Assert.True(Has(resx, prefix + "Step" + n), $"{language} {page}.Step{n}");
                    Assert.True(Has(resx, prefix + "Step" + n + "Title"), $"{language} {page}.Step{n}Title");
                }

                // no gap: nothing past the last step, and no title without its step
                Assert.DoesNotContain(resx.Keys, k => k.StartsWith(prefix + "Step" + (steps + 1), StringComparison.Ordinal));
                // the same shape as English (the same steps, the same optional Immediate)
                Assert.Equal(english.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Order(), resx.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Order());
                foreach (var key in resx.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    Assert.NotEqual(key, resx[key]);                                                   // never the key echoed
                    if (language != "en") Assert.NotEqual(english[key], resx[key]);                  // translated, not copied
                }
            }
        }
    }

    [Fact]
    public void U2_every_quoted_button_or_field_name_is_the_screen_word_in_the_same_language()
    {
        foreach (var language in Languages)
        {
            var help = Resx("PageHelpResource." + language);
            var navigation = Resx("SharedResource." + language);
            foreach (var page in Pages)
            {
                var words = LabelSources[page].SelectMany(source => Resx("Views/" + source + "." + language).Values)
                    .Concat(navigation.Values).Select(v => v.Trim()).ToHashSet(StringComparer.Ordinal);
                var quoted = help.Where(kv => kv.Key.StartsWith("Help." + page + ".", StringComparison.Ordinal))
                    .SelectMany(kv => Quoted.Matches(kv.Value).Select(m => (kv.Key, Word: (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim())))
                    .ToList();
                Assert.NotEmpty(quoted);
                Assert.All(quoted, q => Assert.True(words.Contains(q.Word), $"{language} {q.Key}: “{q.Word}” is not a word of the screen"));
            }
        }
    }

    [Fact]
    public void U3_the_step_icons_are_in_the_catalog_one_per_step()
    {
        var english = Resx("PageHelpResource.en");
        foreach (var page in Pages)
        {
            var steps = Enumerable.Range(1, 5).Count(n => english.ContainsKey("Help." + page + ".Step" + n));
            for (var n = 1; n <= steps; n++)
            {
                Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(page, n));
                Assert.StartsWith("bx-", PageHelpCatalog.StepIcon(page, n));
            }
        }
    }

    [Fact]
    public void U4_the_pages_left_the_help_ledger_and_the_panels_left_the_enter_guard_list()
    {
        var ledger = Source("tests", "page-help-ledger.test.js");
        Assert.DoesNotContain("MAIN_SYNC_3", ledger);
        // no ledger list names any of the seven pages again (a row is `CODE: { owner …` or a pinned "CODE")
        foreach (var page in Pages)
        {
            Assert.DoesNotMatch(new Regex("\\b" + page + "\\s*:\\s*\\{|\"" + page + "\""), ledger);
        }

        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "roadmap", "backlog", "page-help-ledger.md"));
        Assert.All(Pages, page => Assert.DoesNotContain(page, markdown));

        var enter = Source("tests", "diten-enter-primary.test.js");
        foreach (var view in new[] { "CRM/KnowledgePaths/_PresentationNotes.cshtml", "CRM/PotentialThresholds/Index.cshtml", "CRM/VisitEvidenceSettings/Index.cshtml" })
        {
            Assert.DoesNotContain("\"" + view + "\"", enter);
        }
    }

    /// <summary>The decision table: (view, button id, mark).</summary>
    public static TheoryData<string, string, string> Marks => new()
    {
        { "Views/CRM/KnowledgePaths/_PresentationNotes.cshtml", "pnSave", "enter" },          // a draft path step's notes
        { "Views/CRM/PotentialThresholds/Index.cshtml", "pt-save", "enter" },                 // opens the confirm
        { "Views/CRM/PotentialThresholds/Index.cshtml", "pt-save-confirm", "enter" },         // a threshold, edited back any time
        { "Views/CRM/VisitEvidenceSettings/Index.cshtml", "ve-save", "enter" },               // opens the confirm
        { "Views/CRM/VisitEvidenceSettings/Index.cshtml", "ve-save-confirm", "chord" },       // "Save and apply": every rep at once
        { "Views/CRM/VisitEvidenceSettings/Index.cshtml", "ve-nx-submit", "chord" },          // sends to the Workflow approval
        { "Views/CRM/Accounts/_VisitQrCard.cshtml", "vq-regen-confirm", "chord" },            // old posted / printed codes die
        { "Views/CRM/Accounts/_VisitQrCard.cshtml", "vq-revoke-confirm", "chord" },           // the institution is left without a code
        { "Views/CRM/VisitQrCodes/Print.cshtml", "vq-print-go", "enter" }                     // printing changes nothing
    };

    [Theory]
    [MemberData(nameof(Marks))]
    public void U5_each_crm_panel_and_dialog_marks_its_primary_as_decided(string view, string id, string mark)
    {
        var source = Source(view.Split('/'));
        var button = Regex.Match(source, "<button\\b[^>]*\\bid=\"" + Regex.Escape(id) + "\"[^>]*>");
        Assert.True(button.Success, view + " #" + id);
        Assert.Equal(mark, Regex.Match(button.Value, "data-dt-primary=\"([^\"]*)\"").Groups[1].Value);
    }

    [Fact]
    public void U6_the_exception_quick_view_marks_its_reason_confirm_chord_and_tells_the_reader()
    {
        var script = Source("wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings", "visit-evidence-settings.js");
        // the confirm of a return / cancel / early end (the one drawn with the reason field) is chord-marked…
        Assert.Matches(new Regex(@"buttons\.push\(btn\('confirm-' \+ state\.close,[^;]*, false, 'chord'\)\);"), script);
        Assert.Contains("(mark ? ' data-dt-primary=\"' + mark + '\"' : '')", script);
        // …and decorated by the shared module after it is drawn (the module decorates on panel open only)
        Assert.Contains("window.DitenEnterPrimary.decorate(el('oc-actions'))", script);
        // the other quick view actions stay unmarked: no field there, no Enter-primary (UXH-001 §3)
        Assert.Equal(1, Regex.Matches(script, "'chord'").Count);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static bool Has(Dictionary<string, string> resx, string key) => resx.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);

    private static Dictionary<string, string> Resx(string name)
        => XDocument.Load(Path.Combine(WebRoot(), "Resources", name + ".resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine([WebRoot(), .. parts])).Replace("\r\n", "\n");

    private static string RepoRoot() => Path.GetDirectoryName(Path.GetDirectoryName(WebRoot())!)!;

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
