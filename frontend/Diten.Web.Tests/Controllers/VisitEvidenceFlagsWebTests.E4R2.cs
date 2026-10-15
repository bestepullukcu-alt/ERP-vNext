using System.Globalization;
using System.Text.Json;
using Diten.Web.Infrastructure;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-E4R2-FIX-a on the evidence flags page, on the REAL index.js + rep-summary.js in the fake factory: E2-1 the
/// responsive priorities (the start + the actions always, then the flags before the institution, the rep and the kind); the
/// rep summary on the Golden look (the factory's table builder + DtDefaults' own toolbar, client mode, no filter / Save View
/// of its own); E2-2 the custom range shows the culture's date (altInput + the payload's DateFormat, guarded by altFormat,
/// the icon opens it, the red frame reaches the visible field, an outside value reaches it) and sends yyyy-MM-dd;
/// FU-VF-APPLIED-CLEANUP the applied state is the factory handle's live accessor (the fake handle carries the REAL
/// factory's Object.defineProperty getter; its dt.ajax.params() answers the first request only); a tab change re-measures.
/// </summary>
public sealed partial class VisitEvidenceFlagsWebTests
{
    [Fact]
    public void E1_the_flags_stay_on_a_narrow_screen_the_start_and_actions_always_the_kind_duration_exception_go_first()
    {
        var r = FlagsSmoke("team");
        if (r is null) { return; }
        var p = r.Value.GetProperty("priorities");
        string P(int t) => p.GetProperty(t.ToString(CultureInfo.InvariantCulture)).ToString();
        int N(int t) => p.GetProperty(t.ToString(CultureInfo.InvariantCulture)).GetInt32();
        // control 0 · start 1 · rep 2 · institution / doctor 3 · kind 4 · flags 5 · duration 6 · exception 7 · actions -1
        Assert.Equal(("all", "all"), (P(1), p.GetProperty("-1").GetString()));
        Assert.True(N(5) < N(3) && N(3) < N(2), "flags → institution → rep");
        Assert.True(N(2) < N(4) && N(4) < N(6) && N(6) < N(7), "then the kind, the duration, the exception");
        Assert.True(N(5) < N(4), "the flags before the kind (E2-1)");

        // the rep summary: the rep + the actions always, then the rate and the top flag before the counts
        var rp = r.Value.GetProperty("repTable").GetProperty("priorities");
        int R(int t) => rp.GetProperty(t.ToString(CultureInfo.InvariantCulture)).GetInt32();
        Assert.Equal(("all", "all"), (rp.GetProperty("1").GetString(), rp.GetProperty("-1").GetString()));
        Assert.True(R(4) < R(5) && R(5) < R(3) && R(3) < R(2), "rate → top flag → flagged → started");

        // the chips wrap inside their cell on a phone (no horizontal scroll); on a 375 px phone this table's cells run tighter so
        // the start + the flags + the actions fit side by side (FlagsScript has \n line ends)
        var css = FlagsScript("visit-evidence-flags.css");
        Assert.Contains("@media (max-width: 575.98px) {\n    .datatables-visitevidenceflags > :not(caption) > * > * { padding-inline: .5rem; }", css);
        var phone = css[css.IndexOf("@media (max-width: 767.98px) {", StringComparison.Ordinal)..];
        Assert.StartsWith("@media (max-width: 767.98px) {\n    .datatables-visitevidenceflags .vf-chip, .datatables-vf-reps .vf-chip { white-space: normal;", phone);
    }

    [Fact]
    public void E2_the_rep_summary_is_on_the_golden_look_client_mode_with_the_standard_toolbar_and_no_filter_or_save_view_of_its_own()
    {
        var r = FlagsSmoke("team");
        if (r is null) { return; }
        var t = r.Value.GetProperty("repTable");
        Assert.Equal("client", t.GetProperty("dataMode").GetString());
        Assert.False(t.GetProperty("savedView").GetBoolean());
        Assert.False(t.GetProperty("filters").GetBoolean());
        // DtDefaults' own toolbar: the Action menu (print / CSV / Excel / PDF / copy) + column visibility, no add-new, no
        // filter / Save View buttons (those are the visits list's)
        var b = t.GetProperty("buttons");
        Assert.Equal(("DtDefaults.exportButtons", "", 0), (b.GetProperty("toolbar").GetString(), b.GetProperty("addNew").GetString(), b.GetProperty("extra").GetArrayLength()));
        Assert.Equal("[1,2,3,4,5]", b.GetProperty("exportColumns").GetRawText());
        Assert.Equal("[1,2,3,4,5]", b.GetProperty("colvisColumns").GetRawText());
        // the standard layout, info line and paging (nothing switched off any more)
        Assert.False(t.GetProperty("layout").GetBoolean());
        Assert.False(t.GetProperty("pagingOff").GetBoolean());
        Assert.Equal(JsonValueKind.Undefined, t.TryGetProperty("info", out var info) ? info.ValueKind : JsonValueKind.Undefined);
        Assert.DoesNotContain("pageSize", t.GetProperty("url").GetString());                                // client mode: the whole set

        var rep = FlagsScript("rep-summary.js");
        Assert.Contains("buttons: window.DtDefaults.exportButtons('', {}, {}, { exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS }),", rep);
        Assert.DoesNotContain("layout:", rep);
        Assert.DoesNotContain("paging: false", rep);
        Assert.DoesNotContain("info: false", rep);
    }

    [Theory]
    [InlineData("team", "tr")]
    [InlineData("ar", "ar")]
    public void E3_the_custom_range_shows_the_readers_date_and_sends_iso(string mode, string culture)
    {
        var r = FlagsSmoke(mode);
        if (r is null) { return; }
        var range = r.Value.GetProperty("range");
        var expected = DateFieldFormat.Flatpickr(CultureInfo.GetCultureInfo(culture));
        foreach (var side in new[] { "from", "to" })
        {
            var config = range.GetProperty(side);
            Assert.Equal("Y-m-d", config.GetProperty("dateFormat").GetString());                            // what the proxy receives
            Assert.True(config.GetProperty("altInput").GetBoolean());                                       // what the reader sees …
            Assert.Equal(expected, config.GetProperty("altFormat").GetString());                            // … in the culture's format
            Assert.True(config.GetProperty("allowInput").GetBoolean());
        }

        Assert.Equal(["altFormat", "altFormat"], range.GetProperty("guards").EnumerateArray().Select(x => x.GetString()));   // the shared guard, its altInput shape
        Assert.True(range.GetProperty("iconOpens").GetBoolean());                                           // the calendar icon opens the picker
        if (culture == "tr") { Assert.Matches(@"^[dj]\.m\.Y$", expected); }                         // 15.10.2026

        // the 92-day refusal marks the field the reader sees; a value written from outside (Save View / Reset) reaches it
        Assert.True(r.Value.GetProperty("rangeAltInvalid").GetBoolean());
        Assert.True(r.Value.GetProperty("rangeAltInvalidOnLong").GetBoolean());
        Assert.Equal("alt:2026-10-01", r.Value.GetProperty("rangeAltSynced").GetString());

        // the fields are the standard's date-range chips (.filter-from / .filter-to: the shared filter bar gives them their
        // readable width and wraps them one under the other on a phone) — nothing page-level fights the shared chip rule
        var filter = FlagsPartial("_Filter.cshtml");
        Assert.Contains("<div class=\"filter-chip filter-from d-none\" id=\"filterRangeFrom\" data-vf-range>", filter);
        Assert.Contains("<div class=\"filter-chip filter-to d-none\" id=\"filterRangeTo\" data-vf-range>", filter);
        Assert.Contains("placeholder=\"@Localizer[\"CustomFrom\"]\"", filter);
        var shared = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "css", "backbone-custom.css"));
        Assert.Contains("#inlineFilterHost .dt-filter-bar .filter-from, .dt-inline-filter-host .dt-filter-bar .filter-from,", shared);
        Assert.DoesNotContain("filter-range", FlagsScript("visit-evidence-flags.css"));
    }

    [Fact]
    public void E4_the_applied_state_is_the_factory_handles_live_accessor_and_a_tab_change_re_measures_its_table()
    {
        var r = FlagsSmoke("team");
        if (r is null) { return; }
        // the fake handle's dt.ajax.params() answers the FIRST request only: these reads exist only because the page reads
        // the live handle.appliedFilters
        Assert.Equal(new[] { "/CRM/VisitEvidenceFlags/api/summary?period=last30&resourceId=rep-2", "/CRM/VisitEvidenceFlags/api/summary?period=custom&from=2026-09-01&to=2026-09-30" },
            r.Value.GetProperty("summaryLoads").EnumerateArray().Select(x => x.GetString()));
        var js = FlagsScript("index.js");
        Assert.Contains("const appliedFilters = () => (list && list.appliedFilters) || null;", js);
        Assert.DoesNotContain("ajax.params", js);                                                           // the workaround is gone
        Assert.DoesNotContain("bootstrap.Tab", js);
        Assert.Contains("window.DitenTabs?.activate(tab)", js);
        Assert.Equal(["reps:adjust", "reps:recalc", "visits:adjust", "visits:recalc"], r.Value.GetProperty("measured").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("vf-tab-visits", r.Value.GetProperty("repAction").GetProperty("tab").GetString());     // a rep row → the visits tab (DitenTabs)
    }
}
