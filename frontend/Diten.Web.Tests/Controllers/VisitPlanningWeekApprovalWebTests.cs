using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-3A (Web compatibility) — "Save as this week's plan" approves ONLY the selected week (weekStart from the preview's
/// week list), an approved week is locked (band + no save) while the other weeks stay editable, archived plans leave the
/// list, and the three new texts exist in the neutral resx + 7 languages (Turkish with diacritics).
/// </summary>
public sealed class VisitPlanningWeekApprovalWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void Save_this_weeks_plan_sends_the_selected_week_and_an_approved_week_is_locked()
    {
        var details = Read("wwwroot", "assets", "js", "CRM", "VisitPlanning", "details.js");
        Assert.Contains("weekStart: weekStartOf(activeWeek)", details);
        Assert.Contains("const weekInfo = week => ((lastPreview && lastPreview.weeks) || [])[week] || null;", details);
        Assert.Contains("if (readOnly || isWeekApproved(activeWeek)) return Promise.resolve();", details);
        Assert.Contains("if (btn) btn.disabled = readOnly || locked;", details);
        Assert.Contains("applyWeekLock(week);", details);
        Assert.Contains("!s.isFixed", details); // the week's own (new) visits are what gets approved

        var index = Read("wwwroot", "assets", "js", "CRM", "VisitPlanning", "index.js");
        // WP-VP-4J — an archived plan stays out of the list unless the status filter asks for "Archived"
        Assert.Contains("if (sStatus(r) === 'archived' && !normArr(appliedFilters.sessionStatus).includes('archived')) return false;", index);
        Assert.Contains("!(row.approvedWeekCount > 0)", index);
    }

    [Fact]
    public void The_new_texts_exist_in_every_culture()
    {
        var l10n = Read("Views", "CRM", "VisitPlanning", "_IndexL10n.cshtml");
        foreach (var key in new[] { "ApplyWeekConfirm", "WeekEmptyBlocked", "WeekLocked" })
        {
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", l10n);
            foreach (var culture in Cultures)
            {
                var file = "VisitPlanningIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
                var value = XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning", file))
                    .Root!.Elements("data").SingleOrDefault(d => (string?)d.Attribute("name") == key)?.Element("value")?.Value;
                Assert.False(string.IsNullOrWhiteSpace(value), $"{file}:{key}");
            }
        }

        var tr = XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning", "VisitPlanningIndex.tr.resx"));
        Assert.Contains("kilitlenecek", tr.Root!.Elements("data").Single(d => (string?)d.Attribute("name") == "ApplyWeekConfirm").Element("value")!.Value);
        Assert.Contains("onaylandı", tr.Root!.Elements("data").Single(d => (string?)d.Attribute("name") == "WeekLocked").Element("value")!.Value);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { WebRoot() }.Concat(parts).ToArray()));

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
