using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CYC-UI-FIX-1 — the Cycle Periods and Cycle Capacity lists against the golden list template, items 1–3:
/// <list type="bullet">
/// <item>the data mode is declared on the table, and it is the truth (client: no <c>serverSide</c>);</item>
/// <item>the old five-bar block is gone, the shared <c>_TableSkeleton</c> is used, and the page removes it;</item>
/// <item>the <c>#offcanvasDetailsPreview</c> quick view exists with its fields, writes every value as PLAIN TEXT (a
/// period named <c>&lt;script&gt;</c> stays those characters) and makes no request of its own.</item>
/// </list>
/// Measured on the real views and scripts.
/// </summary>
public sealed class CycleListsGoldenFixTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    public static TheoryData<string, string> Lists => new()
    {
        { "CyclePeriods", "dt-cycle-periods" },
        { "CycleCapacities", "dt-cycle-capacities" }
    };

    // ============================================================ 1 · data mode

    [Theory]
    [MemberData(nameof(Lists))]
    public void The_table_declares_client_data_mode_and_the_script_does_not_page_on_the_server(string module, string tableId)
    {
        var table = View(module, "_DataTable.cshtml");
        Assert.Matches(new Regex($@"<table id=""{tableId}"" data-dt-standard=""v2"" data-dt-data-mode=""client"""), table);

        var script = StripComments(Script(module));
        Assert.DoesNotMatch(new Regex(@"\bserverSide\s*:\s*true\b"), script);
        // The whole bounded set is loaded once and handed to DataTables as data.
        Assert.Contains("data: allRows", script);
    }

    // ============================================================ 2 · shared skeleton

    [Theory]
    [MemberData(nameof(Lists))]
    public void The_list_uses_the_shared_table_skeleton_and_removes_it(string module, string _)
    {
        var table = View(module, "_DataTable.cshtml");
        Assert.Contains("<partial name=\"_TableSkeleton\" />", table);
        Assert.DoesNotContain("backbone-skeleton", table);
        Assert.DoesNotContain("skeleton-row", table);

        // The shared placeholder hides its sibling table for as long as it EXISTS: hiding it is not enough.
        var script = Script(module);
        Assert.Contains("document.getElementById('skeleton-loader')?.remove();", script);
        Assert.DoesNotContain("document.getElementById('skeleton-loader')?.classList.add('d-none');", script);
    }

    [Fact]
    public void The_timeline_keeps_its_own_loading_state()
    {
        var index = View("CyclePeriods", "Index.cshtml");
        Assert.Contains("_Timeline.cshtml", index);
        Assert.Contains("getElementById('timelineLoading')", Script("CyclePeriods"));
    }

    // ============================================================ 3 · quick view

    [Fact]
    public void The_period_quick_view_carries_every_field_the_package_names()
    {
        var index = View("CyclePeriods", "Index.cshtml");
        Assert.Contains("id=\"offcanvasDetailsPreview\"", index);
        foreach (var id in new[] { "periodPreviewTitle", "periodPreviewSubtitle", "pvDates", "pvDays", "pvScope", "pvStatus", "pvStatusLine", "pvCapacity", "pvCampaigns", "pvPlannedVisits", "periodPreviewDetails" })
        {
            Assert.Contains($"id=\"{id}\"", index);
        }

        Assert.Contains("@Localizer[\"GoToDetails\"]", index);

        var block = QuickViewBlock("CyclePeriods");
        foreach (var field in new[] { "row.cycleCode", "row.cycleName", "row.startDate", "row.endDate", "dayCount(row)", "row.scopeType", "row.cycleStatus", "row.activatedAt", "row.closedAt", "row.hasCapacity", "row.campaignCount", "row.plannedVisitCount" })
        {
            Assert.Contains(field, block);
        }
    }

    [Fact]
    public void The_capacity_quick_view_carries_every_field_the_package_names()
    {
        var index = View("CycleCapacities", "Index.cshtml");
        Assert.Contains("id=\"offcanvasDetailsPreview\"", index);
        foreach (var id in new[] { "capacityPreviewTitle", "capacityPreviewSubtitle", "cvPeriod", "cvCountry", "cvTypicalVisit", "cvVisits", "cvFte", "cvLimits", "capacityPreviewDetails" })
        {
            Assert.Contains($"id=\"{id}\"", index);
        }

        var block = QuickViewBlock("CycleCapacities");
        foreach (var field in new[] { "row.cycleCode", "row.calendarCountryCode", "row.typicalVisitMinutes", "row.visitModel === 'legacy'", "row.maxPromoProducts", "row.maxNonPromoProducts" })
        {
            Assert.Contains(field, block);
        }

        // The estimate is shown only if the lazy per-row read already answered: pending → "calculating",
        // failed / unresolved → "not calculable" (K-4), never a number of its own.
        Assert.Contains("const c = calcOf(row);", block);
        Assert.Contains("qvText('textCalculating')", block);
        Assert.Contains("qvText('textNotCalculable')", block);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void The_quick_view_writes_plain_text_only_so_a_period_named_script_stays_text(string module, string _)
    {
        var block = StripComments(QuickViewBlock(module));

        foreach (var sink in new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", ".html(", "$(" })
        {
            Assert.DoesNotContain(sink, block);
        }

        // Every value goes through textContent / createTextNode.
        Assert.Matches(new Regex(@"const qvSet = \(id, value\) => \{\s*const el = document\.getElementById\(id\);\s*if \(el\) el\.textContent ="), block);
        Assert.DoesNotMatch(new Regex(@"\.(innerHTML|outerHTML)\s*="), block);

        // The texts the panel borrows from the markup are HTML-encoded strings (.Value), never raw localized HTML.
        var attrs = Regex.Matches(View(module, "Index.cshtml"), @"data-text-[a-z-]+=""@Localizer\[""[A-Za-z]+""\](\.Value)?""");
        Assert.NotEmpty(attrs);
        Assert.All(attrs, attr => Assert.True(attr.Groups[1].Success, $"{attr.Value} must read .Value"));
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void The_quick_view_makes_no_request_of_its_own(string module, string _)
    {
        var block = StripComments(QuickViewBlock(module));
        foreach (var call in new[] { "fetch(", "getJson(", "sendJson(", "XMLHttpRequest", "ajax(", "fetchCalculation(", "fetchRows(" })
        {
            Assert.DoesNotContain(call, block);
        }

        Assert.Contains("allRows.find(", block);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void The_eye_action_opens_the_quick_view_and_falls_back_to_details(string module, string _)
    {
        var script = Script(module);
        Assert.Matches(new Regex(@"if \(quickView\.dataset\.id && !open(Period|Capacity)QuickView\(quickView\.dataset\.id\)\)"), script);
        Assert.Contains("title: L.QuickView", script);
    }

    [Fact]
    public void A_timeline_bar_still_opens_the_period_details_page()
    {
        var script = Script("CyclePeriods");
        Assert.Contains("const timelineBar = event.target.closest('.js-timeline-bar');", script);
        Assert.Contains("window.location.assign(`${pageRoot}/Details/${encodeURIComponent(timelineBar.dataset.id)}`)", script);
    }

    [Fact]
    public void The_new_keys_exist_in_seven_languages()
    {
        foreach (var language in Languages)
        {
            var periods = Resx("CyclePeriods", "CyclePeriodsIndex", language);
            var capacities = Resx("CycleCapacities", "CycleCapacitiesIndex", language);
            foreach (var (values, key) in new[] { (periods, "GoToDetails"), (periods, "StatusLine"), (capacities, "GoToDetails") })
            {
                Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text), $"{language}: missing {key}");
            }
        }

        Assert.Equal("Ayrıntıya git", Resx("CyclePeriods", "CyclePeriodsIndex", "tr")["GoToDetails"]);
        Assert.Equal("Durum çizgisi", Resx("CyclePeriods", "CyclePeriodsIndex", "tr")["StatusLine"]);
    }

    // ============================================================ helpers

    private static string QuickViewBlock(string module)
    {
        var script = Script(module);
        var start = script.IndexOf("// ── quick view (WP-CYC-UI-FIX-1)", StringComparison.Ordinal);
        var end = script.IndexOf("// ── end quick view", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{module}: quick view block markers not found");
        return script[start..end];
    }

    private static string StripComments(string js)
    {
        var noBlock = Regex.Replace(js, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(noBlock, @"(^|[^:])//.*$", "$1", RegexOptions.Multiline);
    }

    private static string View(string module, string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", module, file));

    private static string Script(string module) => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", module, "index.js"));

    private static Dictionary<string, string> Resx(string folder, string family, string language)
    {
        var path = Path.Combine(WebRoot(), "Resources", "Views", "CRM", folder, $"{family}.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
