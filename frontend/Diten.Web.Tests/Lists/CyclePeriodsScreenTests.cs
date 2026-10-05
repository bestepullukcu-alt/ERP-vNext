using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.CyclePeriods;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CYC-UI-1 — the Dönemler screen rules (<see cref="CyclePeriodScreenRules"/>), the unauthorized page, and the seven
/// languages.
/// <para>Sabotage target: make an ACTIVE period's dates editable in <see cref="CyclePeriodScreenRules.FieldStates"/> →
/// <see cref="CP10_Active_Period_Locks_Its_Structural_Fields"/> goes red.</para>
/// </summary>
public sealed class CyclePeriodsScreenTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    private static CyclePeriodRow Row(
        string code, string start, string end, string status = "draft", string scope = "country", string? scopeRef = "TR",
        int year = 2026, int sequence = 1, bool? hasCapacity = null, Guid? id = null)
        => new(id ?? Guid.NewGuid(), code, code + " name", year, sequence, DateOnly.Parse(start), DateOnly.Parse(end),
            scope, scope == "tenant" ? null : scopeRef, status, hasCapacity);

    // ── timeline ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 2025, 2027)]
    [InlineData(2030, 2029, 2031)]
    [InlineData(null, 2025, 2027)]   // no year filter → this year ± 1
    public void CP01_Timeline_Axis_Is_The_Filtered_Year_Plus_Minus_One(int? year, int from, int to)
    {
        var t = CyclePeriodScreenRules.BuildTimeline([], year, Today);

        Assert.Equal(new DateOnly(from, 1, 1), t.AxisStart);
        Assert.Equal(new DateOnly(to, 12, 31), t.AxisEnd);
        Assert.Equal(new[] { from, from + 1, to }, t.Years.Select(y => y.Year));
        Assert.Equal(12, t.QuarterPcts.Count);
    }

    [Fact]
    public void CP02_Today_Line_Only_When_Today_Is_On_The_Axis()
    {
        var on = CyclePeriodScreenRules.BuildTimeline([], 2026, Today);
        var off = CyclePeriodScreenRules.BuildTimeline([], 2030, Today);

        Assert.NotNull(on.TodayPct);
        Assert.InRange(on.TodayPct!.Value, 33.3, 66.7);   // inside the middle year
        Assert.Null(off.TodayPct);
    }

    [Fact]
    public void CP03_Lanes_Are_One_Per_Scope_Address_In_Precedence_Order()
    {
        var rows = new[]
        {
            Row("bu-1", "2026-01-01", "2026-03-31", scope: "business-unit", scopeRef: "B1"),
            Row("tr-1", "2026-01-01", "2026-03-31", scope: "country", scopeRef: "TR"),
            Row("by-1", "2026-01-01", "2026-03-31", scope: "country", scopeRef: "BY"),
            Row("gm-1", "2026-01-01", "2026-12-31", scope: "tenant"),
            Row("tr-2", "2026-04-01", "2026-06-30", scope: "country", scopeRef: "tr")
        };

        var t = CyclePeriodScreenRules.BuildTimeline(rows, 2026, Today);

        Assert.Equal(
            new[] { ("tenant", ""), ("country", "BY"), ("country", "TR"), ("business-unit", "B1") },
            t.Lanes.Select(l => (l.ScopeType, l.ScopeRefKey)));
        Assert.Equal(2, t.Lanes.Single(l => l.ScopeRefKey == "TR").Bars.Count);   // "tr" and "TR" are one lane
    }

    [Fact]
    public void CP04_Bars_Are_Positioned_And_Clipped_To_The_Axis()
    {
        var rows = new[]
        {
            Row("mid", "2026-01-01", "2026-12-31"),
            Row("early", "2024-11-01", "2025-01-31", sequence: 2),
            Row("outside", "2020-01-01", "2020-12-31", sequence: 3)
        };

        var t = CyclePeriodScreenRules.BuildTimeline(rows, 2026, Today);
        var bars = t.Lanes.Single().Bars;

        Assert.DoesNotContain(bars, b => b.CycleCode == "outside");
        var mid = bars.Single(b => b.CycleCode == "mid");
        Assert.Equal(t.Years[1].OffsetPct, mid.OffsetPct, 3);
        Assert.Equal(t.Years[1].WidthPct, mid.WidthPct, 3);
        var early = bars.Single(b => b.CycleCode == "early");
        Assert.Equal(0, early.OffsetPct);
        Assert.True(early.ClippedStart);
        Assert.False(early.ClippedEnd);
    }

    [Fact]
    public void CP05_Gaps_Overlaps_And_Conflicts_Within_A_Lane()
    {
        var rows = new[]
        {
            Row("a", "2026-01-01", "2026-03-31", status: "active", sequence: 1),
            Row("b", "2026-03-15", "2026-06-30", status: "active", sequence: 2),   // active ∩ active → conflict
            Row("c", "2026-06-01", "2026-07-15", status: "draft", sequence: 3),    // active ∩ draft → overlap
            Row("d", "2026-09-01", "2026-12-31", status: "draft", sequence: 4),    // 16 Jul – 31 Aug → gap
            Row("z", "2026-07-20", "2026-08-10", status: "closed", sequence: 5)    // closed: drawn, ignored
        };

        var lane = CyclePeriodScreenRules.BuildTimeline(rows, 2026, Today).Lanes.Single();

        Assert.Equal(5, lane.Bars.Count);
        var conflict = Assert.Single(lane.Marks, m => m.Kind == "conflict");
        Assert.Equal((new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 31)), (conflict.From, conflict.To));
        Assert.Equal(new[] { "a", "b" }, conflict.Codes);
        var overlap = Assert.Single(lane.Marks, m => m.Kind == "overlap");
        Assert.Equal((new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), (overlap.From, overlap.To));
        var gap = Assert.Single(lane.Marks, m => m.Kind == "gap");
        Assert.Equal((new DateOnly(2026, 7, 16), new DateOnly(2026, 8, 31)), (gap.From, gap.To));
    }

    [Fact]
    public void CP06_Different_Levels_Never_Mark_Each_Other()
    {
        var rows = new[]
        {
            Row("tr", "2026-01-01", "2026-12-31", status: "active", scope: "country", scopeRef: "TR"),
            Row("b1", "2026-03-01", "2026-04-30", status: "active", scope: "business-unit", scopeRef: "B1")
        };

        var t = CyclePeriodScreenRules.BuildTimeline(rows, 2026, Today);

        Assert.All(t.Lanes, l => Assert.Empty(l.Marks));
    }

    [Fact]
    public void CP07_Timeline_Filters_Narrow_Rows()
    {
        var rows = new[]
        {
            Row("tr", "2026-01-01", "2026-03-31", status: "active", scope: "country", scopeRef: "TR"),
            Row("by", "2026-01-01", "2026-03-31", status: "draft", scope: "country", scopeRef: "BY"),
            Row("gm", "2026-01-01", "2026-03-31", status: "closed", scope: "tenant")
        };

        Assert.Equal(new[] { "tr" }, CyclePeriodScreenRules.Filter(rows, null, "tr", null).Select(r => r.CycleCode));
        Assert.Equal(new[] { "gm" }, CyclePeriodScreenRules.Filter(rows, "tenant", null, null).Select(r => r.CycleCode));
        Assert.Equal(new[] { "tr", "by" }, CyclePeriodScreenRules.Filter(rows, null, null, ["active", "draft"]).Select(r => r.CycleCode));
        Assert.Equal(3, CyclePeriodScreenRules.Filter(rows, "", "", []).Count);
    }

    // ── field states / actions ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CP08_New_Period_Edits_Everything()
    {
        var f = CyclePeriodScreenRules.FieldStates(null, isNew: true);
        Assert.True(f.Code && f.Name && f.Description && f.Year && f.Sequence && f.Dates && f.Scope);
        Assert.False(f.ReadOnly);
    }

    [Fact]
    public void CP09_Draft_Locks_Code_After_Save_And_Scope()
    {
        var f = CyclePeriodScreenRules.FieldStates("draft", isNew: false);
        Assert.False(f.Code);     // K-2: the suggested code is read-only once saved
        Assert.False(f.Scope);    // the scope never changes
        Assert.True(f.Dates && f.Year && f.Sequence && f.Name && f.Description);
        Assert.False(f.OfferCloseAndReopen);
    }

    [Fact]
    public void CP10_Active_Period_Locks_Its_Structural_Fields()
    {
        var f = CyclePeriodScreenRules.FieldStates("active", isNew: false);
        Assert.False(f.Dates);
        Assert.False(f.Year);
        Assert.False(f.Sequence);
        Assert.False(f.Scope);
        Assert.False(f.Code);
        Assert.True(f.Name);
        Assert.True(f.Description);
        Assert.True(f.OfferCloseAndReopen);   // K-3
        Assert.False(f.ReadOnly);
    }

    [Fact]
    public void CP11_Closed_Period_Is_Read_Only()
    {
        var f = CyclePeriodScreenRules.FieldStates("closed", isNew: false);
        Assert.True(f.ReadOnly);
        Assert.False(f.Name || f.Description || f.Dates || f.Code || f.Scope || f.Year || f.Sequence);
    }

    [Theory]
    [InlineData("draft", true, true, true)]     // E3: a draft can be closed too
    [InlineData("active", false, true, true)]
    [InlineData("closed", false, false, false)]
    public void CP12_Lifecycle_Actions_By_Status(string status, bool activate, bool close, bool edit)
    {
        Assert.Equal(new CyclePeriodActions(activate, close, edit),
            CyclePeriodScreenRules.Actions(status, canActivate: true, canManage: true));
        Assert.Equal(new CyclePeriodActions(false, false, false),
            CyclePeriodScreenRules.Actions(status, canActivate: false, canManage: false));
    }

    // ── panel checks ─────────────────────────────────────────────────────────────────────────────────────────────

    private static CyclePeriodDraft Draft(string start, string end, string scope = "country", string? scopeRef = "TR",
        int year = 2026, int sequence = 4, Guid? id = null)
        => new(id, year, sequence, DateOnly.Parse(start), DateOnly.Parse(end), scope, scopeRef);

    [Fact]
    public void CP13_End_Must_Be_After_Start()
    {
        var same = CyclePeriodScreenRules.Check(Draft("2026-10-01", "2026-10-01"), []);
        var before = CyclePeriodScreenRules.Check(Draft("2026-10-02", "2026-10-01"), []);
        var fine = CyclePeriodScreenRules.Check(Draft("2026-10-01", "2026-10-02"), []);

        Assert.Contains(same, w => w.Code == "end_not_after_start" && w.Tone == "error");
        Assert.Contains(before, w => w.Code == "end_not_after_start");
        Assert.DoesNotContain(fine, w => w.Code == "end_not_after_start");
    }

    [Fact]
    public void CP14_Sequence_Taken_In_Same_Scope_And_Year_Closed_Included()
    {
        var closed = Row("tr-2026-04", "2026-01-01", "2026-01-31", status: "closed", sequence: 4);
        var otherScope = Row("by-2026-05", "2026-01-01", "2026-01-31", scopeRef: "BY", sequence: 5);
        var rows = new[] { closed, otherScope };

        var taken = CyclePeriodScreenRules.Check(Draft("2026-10-01", "2026-12-31", sequence: 4), rows);
        var free = CyclePeriodScreenRules.Check(Draft("2026-10-01", "2026-12-31", sequence: 5), rows);
        var self = CyclePeriodScreenRules.Check(Draft("2026-10-01", "2026-12-31", sequence: 4, id: closed.CyclePeriodId), rows);

        var w = Assert.Single(taken, x => x.Code == "sequence_taken");
        Assert.Equal("tr-2026-04", Assert.Single(w.Periods).CycleCode);
        Assert.DoesNotContain(free, x => x.Code == "sequence_taken");
        Assert.DoesNotContain(self, x => x.Code == "sequence_taken");
        Assert.Equal(1, CyclePeriodScreenRules.NextSequence(rows, "country", "TR", 2026));
        Assert.Equal(5, CyclePeriodScreenRules.NextSequence(
            [.. Enumerable.Range(1, 4).Select(n => Row($"x{n}", "2026-01-01", "2026-01-02", sequence: n))], "country", "tr", 2026));
    }

    [Fact]
    public void CP15_Active_Overlap_In_Same_Scope_Is_Linked_Other_Levels_Are_Info()
    {
        var active = Row("tr-act", "2026-09-01", "2026-12-31", status: "active", sequence: 9);
        var draft = Row("tr-dr", "2026-09-01", "2026-12-31", status: "draft", sequence: 8);
        var closed = Row("tr-cl", "2026-09-01", "2026-12-31", status: "closed", sequence: 7);
        var bu = Row("b1", "2026-11-01", "2026-11-30", status: "active", scope: "business-unit", scopeRef: "B1", sequence: 1);

        var warnings = CyclePeriodScreenRules.Check(Draft("2026-10-01", "2026-12-31"), [active, draft, closed, bu]);

        var overlap = Assert.Single(warnings, w => w.Code == "active_overlap");
        Assert.Equal("warning", overlap.Tone);
        Assert.Equal(active.CyclePeriodId, Assert.Single(overlap.Periods).CyclePeriodId);   // the link target
        var info = Assert.Single(warnings, w => w.Code == "other_level_overlap");
        Assert.Equal("info", info.Tone);
        Assert.Equal("b1", Assert.Single(info.Periods).CycleCode);
    }

    // ── finder (K-6) ─────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("resolved", "resolved", "success", "FinderResolved")]
    [InlineData("none", "none", "secondary", "FinderNone")]
    [InlineData("ambiguous", "ambiguous", "warning", "FinderAmbiguous")]
    [InlineData(null, "none", "secondary", "FinderNone")]
    public void CP16_Finder_Tells_The_Three_Outcomes_Apart(string? outcome, string expected, string tone, string key)
        => Assert.Equal(new CyclePeriodFinderView(expected, tone, key), CyclePeriodScreenRules.Finder(outcome));

    // ── band (E11) / calendar months ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CP17_Band_Lists_Open_Periods_Without_A_Capacity_Only()
    {
        var rows = new[]
        {
            Row("a", "2026-01-01", "2026-03-31", status: "active", hasCapacity: false),
            Row("b", "2026-04-01", "2026-06-30", status: "draft", hasCapacity: false, sequence: 2),
            Row("c", "2026-07-01", "2026-09-30", status: "closed", hasCapacity: false, sequence: 3),
            Row("d", "2026-10-01", "2026-12-31", status: "active", hasCapacity: true, sequence: 4),
            Row("e", "2027-01-01", "2027-03-31", status: "draft", hasCapacity: null, sequence: 1)   // unknown ≠ missing
        };

        Assert.Equal(new[] { "a", "b" }, CyclePeriodScreenRules.OpenWithoutCapacity(rows).Select(r => r.CycleCode));
    }

    [Fact]
    public void CP18_Calendar_Months_Are_Clipped_At_The_Period_Edges()
    {
        var months = CyclePeriodScreenRules.Months(new DateOnly(2026, 10, 15), new DateOnly(2026, 12, 10));

        Assert.Equal(
            new[] { (2026, 10, 15, 31), (2026, 11, 1, 30), (2026, 12, 1, 10) },
            months.Select(m => (m.Year, m.Month, m.From.Day, m.To.Day)));
        Assert.Equal(57, CyclePeriodScreenRules.DayCount(new DateOnly(2026, 10, 15), new DateOnly(2026, 12, 10)));
        Assert.Empty(CyclePeriodScreenRules.Months(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
    }

    // ── payload / unauthorized ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CP19_Only_The_Chosen_Scope_Reference_Is_Posted()
    {
        var payload = CyclePeriodsController.ToPayload(new Diten.Web.Models.CRM.CyclePeriodEditViewModel
        {
            CycleCode = "TR-2026-04", CycleName = "n", Year = 2026, SequenceInYear = 4,
            StartDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(3)),
            EndDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            ScopeType = "country", CountryScope = "TR", LegalEntityId = Guid.NewGuid(), BusinessUnitId = "B1",
            BusinessUnitCountryContext = "TR"
        }, includeExpectedVersion: false);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        Assert.Contains("\"countryScope\":\"TR\"", json);
        Assert.Contains("\"legalEntityId\":null", json);
        Assert.Contains("\"businessUnitId\":null", json);
        Assert.Contains("\"businessUnitCountryContext\":null", json);
        Assert.Contains("2026-10-01T00:00:00+00:00", json);   // the picked day, not the server-offset instant
    }

    [Fact]
    public async Task CP20_Unauthorized_Pages_Answer_403_Without_A_Shell_Or_Redirect()
    {
        var controller = new CyclePeriodsController(
            new HttpClient(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:1" }).Build(),
            NullLogger<CyclePeriodsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        var index = await controller.Index(CancellationToken.None);
        var details = await controller.Details(Guid.NewGuid(), CancellationToken.None);
        var overview = await controller.Overview(2026, null, null, null, CancellationToken.None);

        Assert.Equal(403, Assert.IsType<StatusCodeResult>(index).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(details).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(overview).StatusCode);
    }

    [Fact]
    public void CP21_Create_And_Edit_Pages_Are_Gone_The_Panel_Replaced_Them()
    {
        var root = Path.Combine(RepoRoot(), "frontend", "Diten.Web");
        Assert.False(File.Exists(Path.Combine(root, "Views", "CRM", "CyclePeriods", "Create.cshtml")));
        Assert.False(File.Exists(Path.Combine(root, "Views", "CRM", "CyclePeriods", "Edit.cshtml")));
        var index = File.ReadAllText(Path.Combine(root, "Views", "CRM", "CyclePeriods", "Index.cshtml"));
        Assert.Contains("_PeriodPanel.cshtml", index);
        Assert.Contains("_Timeline.cshtml", index);
        Assert.Contains("skeleton-loader", File.ReadAllText(Path.Combine(root, "Views", "CRM", "CyclePeriods", "_DataTable.cshtml")));
    }

    // ── seven languages ──────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void CP22_All_Seven_Languages_Carry_The_Same_Keys()
    {
        var reference = Values("en").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var keys = Values(language).Keys.ToHashSet(StringComparer.Ordinal);
            Assert.True(reference.SetEquals(keys),
                $"{language}: missing [{string.Join(", ", reference.Except(keys))}] extra [{string.Join(", ", keys.Except(reference))}]");
        }
    }

    [Fact]
    public void CP23_Every_New_Key_Has_A_Real_Value_In_Every_Language()
    {
        foreach (var language in Languages)
        {
            var values = Values(language);
            foreach (var key in CyclePeriodsL10nKeys.NewKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}: '{key}' missing");
                // A key echoing its own name is the localizer's silent fallback. A plain-word key may legitimately
                // equal its English or French value ("Campaigns", "Dates"); Turkish never does, so it is the probe.
                if (language == "tr")
                {
                    Assert.NotEqual(key, value);
                }
            }
        }

        // Turkish keeps its diacritics (a known regression class: ASCII-folded Turkish).
        var tr = Values("tr");
        Assert.Equal("Dönem kapasiteleri", tr["CycleCapacities"]);
        Assert.Equal("Çakışma denetimi", tr["ChecksSection"]);
    }

    [Fact]
    public void CP24_Every_Key_The_Scripts_Read_Is_Bridged()
    {
        var bridged = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "CRM", "CyclePeriods", "_IndexL10n.cshtml"));
        var legacy = Regex.Matches(bridged, @"^\s*([A-Z][A-Za-z]+) = ", RegexOptions.Multiline).Select(m => m.Groups[1].Value);
        var known = legacy.Concat(CyclePeriodsL10nKeys.Bridge).ToHashSet(StringComparer.Ordinal);

        var scripts = Directory.GetFiles(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM", "CyclePeriods"), "*.js");
        var used = scripts.SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bL(?:\(\))?\.([A-Z][A-Za-z]+)").Select(m => m.Groups[1].Value)).ToHashSet();

        Assert.Empty(used.Except(known));
        Assert.All(CyclePeriodsL10nKeys.Bridge, key => Assert.Contains(key, CyclePeriodsL10nKeys.NewKeys));
    }

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "CyclePeriods",
            $"CyclePeriodsIndex.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
