using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.VisitEvidenceFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-FLAGS-STD (K-STD) — the evidence flags page on the Golden Compact list standard, on the REAL views,
/// proxy, resx and scripts: (1) the visits table is the shared v2 shell in SERVER mode with the selection DECLARED off (read
/// only), the rep summary the second v2 table in CLIENT mode, the standard filter / header / L10n bridge / quick view; (2)
/// the proxy turns the list contract (start / length + the filter keys) into the CRM's window / page query (92 / 93 days,
/// presets on a pinned clock, flags csv, scope → onlyFlagged) and refuses what the CRM would; the summary read carries no
/// page size; the export writes every match in the reader's words; (3) the page in a fake factory: the server-mode options,
/// the cells, ONE filter state for the card and both tables (Save View), the 92-day Apply guard, a rep row filters the
/// visits, the quick view, the empty state, Arabic.
/// </summary>
public sealed partial class VisitEvidenceFlagsWebTests
{
    private static readonly DateTimeOffset StdToday = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    // ═══ 1 · the standard's markup ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void S1_the_visits_table_is_the_shared_v2_shell_in_server_mode_read_only_and_the_rep_summary_a_client_v2_table()
    {
        var dataTable = FlagsPartial("_DataTable.cshtml");
        Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=\"listShell\" />", dataTable);
        Assert.Contains("TableId = \"dt-visitevidenceflags\"", dataTable);
        Assert.Contains("DataMode = \"server\"", dataTable);
        Assert.Contains("HasSelection = false", dataTable);                                                    // read only: the selection is DECLARED off
        var shell = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "Components", "DataTable", "_ListShell.cshtml"));
        Assert.Contains("data-dt-standard=\"v2\"", Regex.Match(shell, "<table [^>]*>").Value);

        var view = FlagsView();
        foreach (var marker in new[] { "<partial name=\"~/Views/CRM/VisitEvidenceFlags/_Filter.cshtml\" model=\"Model\" />", "<partial name=\"~/Views/CRM/VisitEvidenceFlags/_DataTable.cshtml\" />",
                     "<partial name=\"~/Views/CRM/VisitEvidenceFlags/_IndexL10n.cshtml\" />", "id=\"offcanvasDetailsPreview\"", "Layout = \"_LayoutTenantShell\";",
                     "~/assets/js/CRM/VisitEvidenceFlags/index.l10n.js", "~/assets/js/CRM/VisitEvidenceFlags/rep-summary.js", "~/assets/js/CRM/VisitEvidenceFlags/index.js" })
        {
            Assert.Contains(marker, view);
        }

        Assert.DoesNotContain("_BulkActionBar", view);                                                          // nothing to act on in bulk
        Assert.DoesNotContain("visit-evidence-flags.js", view);
        Assert.DoesNotContain("window.VisitEvidenceFlagsL10n", view);                                          // the standard L10n bridge only
        Assert.True(view.IndexOf("rep-summary.js", StringComparison.Ordinal) < view.IndexOf("VisitEvidenceFlags/index.js", StringComparison.Ordinal));
        // the summary card ABOVE the tab card (K-TABS), the tab card above the two panes, the quick view after
        Assert.True(view.IndexOf("id=\"vf-summary\"", StringComparison.Ordinal) < view.IndexOf("<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"tabCard\" />", StringComparison.Ordinal));
        Assert.True(view.IndexOf("_TabCard.cshtml", StringComparison.Ordinal) < view.IndexOf("id=\"vf-pane-visits\"", StringComparison.Ordinal));
        Assert.True(view.IndexOf("id=\"vf-pane-visits\"", StringComparison.Ordinal) < view.IndexOf("id=\"vf-pane-reps\"", StringComparison.Ordinal));

        // the rep summary (W3E-E4R2-FIX-a): the same shared table card, CLIENT mode (a bounded set), read only
        var reps = FlagsPartial("_RepSummaryTable.cshtml");
        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceFlags/_RepSummaryTable.cshtml\" />", view);
        Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=\"repShell\" />", reps);
        Assert.Contains("TableId = \"dt-vf-reps\"", reps);
        Assert.Contains("DataMode = \"client\"", reps);
        Assert.Contains("Slug = \"vf-reps\"", reps);
        Assert.Contains("HasSelection = false", reps);

        // the filter: the inline collapse, Select2 chips (the flag types MANY), the shared date fields, the scope switch
        var filter = FlagsPartial("_Filter.cshtml");
        Assert.Contains("id=\"inlineFilterCollapse\"", filter);
        Assert.Contains("class=\"pt-0 pb-3\"", filter);
        foreach (var chip in new[] { "filterPeriod", "filterRep", "filterAccount" })
        {
            Assert.Matches("<select id=\"" + chip + "\" class=\"form-select form-select-sm select2\" data-placeholder=", filter);
        }

        Assert.Matches("<select id=\"filterFlags\" class=\"form-select form-select-sm select2\" multiple=\"multiple\" data-placeholder=\"@Localizer\\[\"FlagTypeLabel\"\\]\">", filter);
        Assert.DoesNotContain("qr_missing", filter);                                                           // "QR not scanned" is not a flag
        Assert.Contains("<input type=\"hidden\" id=\"filterScope\" value=\"\" />", filter);
        Assert.Contains("id=\"vf-only\" checked", filter);
        Assert.Contains("<option value=\"\"></option>", filter);                                             // the baseline "" of each select

        // the factory call: server mode, the export, NO page-written serverSide / ajax.data / ext.search, no page cap
        var js = FlagsScript("index.js");
        Assert.Contains("list = await window.DitenDataTable.createList({", js);
        Assert.Contains("dataMode: 'server'", js);
        Assert.Contains("const endpoint = '/CRM/VisitEvidenceFlags/api';", js);
        Assert.Contains("export: { mode: 'server', url: endpoint + '/flags/export'", js);
        Assert.Contains("byId('btnFilterApply')?.addEventListener('click', guardApply, true);", js);         // the 92-day guard runs in the capture phase
        var code = Regex.Replace(Regex.Replace(js, @"/\*[\s\S]*?\*/", ""), @"(^|[^:])//.*$", "$1", RegexOptions.Multiline);
        Assert.DoesNotContain("serverSide", code);
        Assert.DoesNotContain("ext.search", code);
        Assert.DoesNotMatch(@"\bdata\s*:\s*function", code);
        Assert.DoesNotMatch(@"pageSize=\d", code);

        var rep = FlagsScript("rep-summary.js");
        Assert.Contains("window.DitenDataTable.createCrudTable({", rep);
        Assert.Contains("dataMode: 'client'", rep);
        Assert.DoesNotContain("pageSize", rep);                                                                // client mode: never a page size
        Assert.DoesNotContain("savedView", rep);                                                               // no Save View of its own: it follows the list
    }

    // ═══ 2 · the proxy: the list contract → the CRM query ═════════════════════════════════════════════════════

    [Fact]
    public async Task S2_the_proxy_turns_start_length_and_the_filter_keys_into_the_crm_window_and_page()
    {
        const string Whole = "{\"data\":{\"items\":[{}],\"total\":412}}";
        const string Page = "{\"data\":{\"items\":[{\"executionId\":\"e1\"}],\"total\":37,\"page\":3,\"pageSize\":10}}";

        // the baseline: the last 7 days on the pinned clock, only flagged (scope ""), start 20 / length 10 → page 3
        var (gw, c) = ArrangeStd([Read], path => path.Contains("pageSize=1", StringComparison.Ordinal) && path.Contains("onlyFlagged=false", StringComparison.Ordinal) ? Whole : Page);
        var ok = Assert.IsType<OkObjectResult>(await c.List(20, 10, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Equal(new[]
        {
            "/api/crm/visit-evidence-flags?from=2026-10-08&to=2026-10-14&onlyFlagged=true&page=3&pageSize=10",
            "/api/crm/visit-evidence-flags?from=2026-10-08&to=2026-10-14&onlyFlagged=false&page=1&pageSize=1"   // total = every visit of the window
        }, gw.Paths());
        var body = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value)).RootElement.GetProperty("data");
        Assert.Equal((412, 37, 1), (body.GetProperty("total").GetInt64(), body.GetProperty("filteredTotal").GetInt64(), body.GetProperty("items").GetArrayLength()));

        // every filter key → its CRM parameter: the period, the rep, the flags (repeated) as ONE csv, the institution, scope "all"
        var (gw2, c2) = ArrangeStd([ReadAll], _ => Page);
        await c2.List(0, 25, null, null, null, "last30", null, null, "rep-2", ["location_far", "short_visit", "location_far", " "], "acc-1", "all", CancellationToken.None);
        Assert.Equal("/api/crm/visit-evidence-flags?from=2026-09-15&to=2026-10-14&resourceId=rep-2&flags=location_far%2Cshort_visit&accountId=acc-1&onlyFlagged=false&page=1&pageSize=25", gw2.Paths()[0]);

        // every visit, nothing narrowed → ONE call (total = filteredTotal)
        var (gw3, c3) = ArrangeStd([Read], _ => Page);
        var all = Assert.IsType<OkObjectResult>(await c3.List(0, 10, null, null, null, "month", null, null, null, null, null, "all", CancellationToken.None));
        Assert.Equal(new[] { "/api/crm/visit-evidence-flags?from=2026-10-01&to=2026-10-14&onlyFlagged=false&page=1&pageSize=10" }, gw3.Paths());
        var allBody = JsonDocument.Parse(JsonSerializer.Serialize(all.Value)).RootElement.GetProperty("data");
        Assert.Equal(allBody.GetProperty("total").GetInt64(), allBody.GetProperty("filteredTotal").GetInt64());

        // a custom range of exactly 92 days goes; 93 does not (nor reversed / missing / an unknown period)
        var (gw4, c4) = ArrangeStd([Read], _ => Page);
        await c4.List(0, 10, null, null, null, "custom", "2026-07-15", "2026-10-14", null, null, null, "all", CancellationToken.None);
        Assert.StartsWith("/api/crm/visit-evidence-flags?from=2026-07-15&to=2026-10-14&", gw4.Paths()[0]);

        var (gw5, c5) = ArrangeStd([Read], _ => Page);
        foreach (var (period, from, to) in new[] { ("custom", "2026-07-14", "2026-10-14"), ("custom", "2026-10-14", "2026-10-01"), ("custom", "2026-10-01", null), ("year", null, null) })
        {
            var refused = await c5.List(0, 10, null, null, null, period, from, to, null, null, null, null, CancellationToken.None);
            Assert.Equal(400, Status(refused));
            Assert.Contains("visit_evidence_flags_range_invalid", JsonSerializer.Serialize(((ObjectResult)refused).Value));
        }

        // the list contract's own refusals: a sort key (the CRM orders by the start), a page size outside 1–100, a bad direction
        Assert.Equal(400, Status(await c5.List(0, 10, null, "startedAt", "desc", null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c5.List(0, 101, null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c5.List(0, 0, null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c5.List(0, 10, null, null, "sideways", null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw5.Requests);                                                                            // none of them reached the CRM

        var (gw6, c6) = ArrangeStd(PhotoKeys, _ => Page);
        Assert.Equal(403, Status(await c6.List(0, 10, null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw6.Requests);
    }

    [Fact]
    public async Task S3_the_summary_read_follows_the_same_period_and_rep_and_never_carries_a_page_size()
    {
        var (gw, c) = ArrangeStd([Read], _ => "{\"data\":{\"started\":412,\"reps\":[]}}");
        Assert.Equal(200, Status(await c.Summary(null, null, null, null, CancellationToken.None)));
        Assert.Equal(200, Status(await c.Summary("custom", "2026-09-01", "2026-09-30", "rep-2", CancellationToken.None)));
        Assert.Equal(new[]
        {
            "/api/crm/visit-evidence-flags/summary?from=2026-10-08&to=2026-10-14",
            "/api/crm/visit-evidence-flags/summary?from=2026-09-01&to=2026-09-30&resourceId=rep-2"
        }, gw.Paths());
        Assert.All(gw.Paths(), p => Assert.DoesNotContain("pageSize", p));                                     // client mode: the whole rep set
        Assert.Equal(400, Status(await c.Summary("custom", "2026-01-01", "2026-12-31", null, CancellationToken.None)));
        Assert.Equal(2, gw.Requests.Count);
        var (gw2, c2) = ArrangeStd([], _ => "{}");
        Assert.Equal(403, Status(await c2.Summary(null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw2.Requests);
    }

    [Fact]
    public async Task S4_the_export_writes_every_matching_visit_page_by_page_in_the_readers_words_and_refuses_over_50000()
    {
        static string Rows(int from, int count, long total) => "{\"data\":{\"total\":" + total + ",\"items\":[" + string.Join(",", Enumerable.Range(from, count).Select(i =>
            "{\"executionId\":\"e" + i + "\",\"startedAt\":\"2026-10-14T07:42:00Z\",\"repName\":\"Sema Kara\",\"kind\":\"in_person\",\"durationSeconds\":100,"
            + "\"target\":{\"type\":\"contact\",\"name\":\"Dr. Hakan Öztürk\",\"accountName\":\"Kardiyoloji Polikliniği\"},"
            + "\"location\":{\"distanceMeters\":198290},\"flags\":[\"short_visit\",\"location_far\"],\"qr\":{\"status\":\"missing\"},\"exceptionId\":" + (i == 0 ? "\"x\"" : "null") + "}")) + "]}}";
        var (gw, c) = ArrangeStd([Read], path => path.Contains("page=1&", StringComparison.Ordinal) ? Rows(0, 100, 101) : Rows(100, 1, 101), "tr");
        var file = Assert.IsType<FileContentResult>(await c.Export("csv", ["repName,target,flags,durationSeconds,exceptionId"], null, null, null, "last30", null, null, null, ["location_far"], null, null, CancellationToken.None));
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.StartsWith("visit-evidence-flags-", file.FileDownloadName);
        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart('﻿').Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(102, csv.Length);                                                                         // the header + EVERY match (2 pages)
        var tr = FlagsResx("tr");
        Assert.Equal(string.Join(",", tr["ColRep"], tr["ColTarget"], tr["ColFlags"], tr["ColDuration"], tr["ColException"]), csv[0]);
        // the chips' words + values in the screen's order (location → duration), "QR not scanned" last; the institution · doctor
        var expectedFlags = tr["Flag_location_far"] + " · " + Fmt(tr["UnitKm"], "198,3") + ", " + tr["Flag_short_visit"] + " · " + Fmt(tr["MinSec"], 1, 40) + ", " + tr["Chip_qr_missing"];
        Assert.Equal("Sema Kara,Kardiyoloji Polikliniği · Dr. Hakan Öztürk,\"" + expectedFlags + "\"," + Fmt(tr["MinSec"], 1, 40) + "," + tr["ExceptionLink"], csv[1]);
        Assert.Contains(gw.Paths(), p => p == "/api/crm/visit-evidence-flags?from=2026-09-15&to=2026-10-14&flags=location_far&onlyFlagged=true&page=2&pageSize=100");
        Assert.All(gw.Paths(), p => Assert.DoesNotContain("start=", p));

        // more than 50 000 → 413 EXPORT_TOO_LARGE after ONE read, nothing written
        var (gw2, c2) = ArrangeStd([Read], _ => Rows(0, 1, 50_001));
        var big = Assert.IsType<ObjectResult>(await c2.Export("xlsx", null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Equal(413, big.StatusCode);
        Assert.Contains("EXPORT_TOO_LARGE", JsonSerializer.Serialize(big.Value));
        Assert.Single(gw2.Requests);

        // an unknown column / format / a 93-day window → 400, no read; no key → 403
        var (gw3, c3) = ArrangeStd([Read], _ => Rows(0, 1, 1));
        Assert.Equal(400, Status(await c3.Export("csv", ["repName,payload"], null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.Export("pdf", null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.Export("csv", null, null, null, null, "custom", "2026-07-14", "2026-10-14", null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw3.Requests);
        Assert.Equal(403, Status(await ArrangeStd([], _ => "{}").Controller.Export("csv", null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(VisitEvidenceFlagsController.ExportKeys, ["startedAt", "repName", "target", "kind", "flags", "durationSeconds", "exceptionId"]);
    }

    [Theory]
    [InlineData(null, null, null, "2026-10-08", "2026-10-14")]
    [InlineData("", null, null, "2026-10-08", "2026-10-14")]
    [InlineData("last7", null, null, "2026-10-08", "2026-10-14")]
    [InlineData("LAST30", null, null, "2026-09-15", "2026-10-14")]
    [InlineData("month", null, null, "2026-10-01", "2026-10-14")]
    [InlineData("custom", "2026-07-15", "2026-10-14", "2026-07-15", "2026-10-14")]
    [InlineData("custom", "2026-10-14", "2026-10-14", "2026-10-14", "2026-10-14")]
    [InlineData("custom", "2026-07-14", "2026-10-14", null, null)]
    [InlineData("custom", "2026-10-15", "2026-10-14", null, null)]
    [InlineData("custom", "14.10.2026", "2026-10-14", null, null)]
    [InlineData("custom", null, "2026-10-14", null, null)]
    [InlineData("quarter", null, null, null, null)]
    public void S5_the_period_window_is_the_twin_of_the_page_rule_92_days_both_ends_included(string? period, string? from, string? to, string? wantFrom, string? wantTo)
    {
        var r = VisitEvidenceFlagsController.ResolveRange(period, from, to, DateOnly.FromDateTime(StdToday.UtcDateTime));
        Assert.Equal((wantFrom, wantTo), (r.From, r.To));
        Assert.Equal(wantFrom is null, r.Error is not null);
    }

    // ═══ 3 · the page in a fake factory ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void S6_the_team_page_one_filter_state_for_the_card_and_both_tables_the_guard_the_rep_filter_and_the_quick_view()
    {
        var r = FlagsSmoke("team");
        if (r is null) { return; }
        var s = r.Value;
        var tr = FlagsResx("tr");

        // the server-mode list as the factory receives it
        var o = s.GetProperty("options");
        Assert.Equal(("server", "/CRM/VisitEvidenceFlags/api/flags", "server", "/CRM/VisitEvidenceFlags/api/flags/export"),
            (o.GetProperty("dataMode").GetString(), o.GetProperty("ajax").GetString(), o.GetProperty("exportMode").GetString(), o.GetProperty("exportUrl").GetString()));
        Assert.Equal("filterPeriod:period:single,vf-from:from:text,vf-to:to:text,filterRep:resourceId:single,filterFlags:flags:multi,filterAccount:accountId:single,filterScope:scope:text",
            o.GetProperty("fields").GetString());
        Assert.Equal(("CRM/VisitEvidenceFlags", "[]", "[1,2,3,4,5,6,7]"), (o.GetProperty("savedView").GetString(), o.GetProperty("baseOrder").GetString(), o.GetProperty("saveViewColumns").GetString()));
        Assert.Equal(("offcanvasDetailsPreview/quickView", "function", ""), (o.GetProperty("quickView").GetString(), o.GetProperty("onResponse").GetString(), o.GetProperty("addNew").GetString()));
        Assert.False(o.GetProperty("anySortable").GetBoolean());                                               // the CRM orders by the start
        Assert.False(o.GetProperty("searching").GetBoolean());                                                 // the CRM has no text search
        Assert.Equal(("[]", false, "undefined"), (o.GetProperty("order").GetString(), o.GetProperty("serverSideWritten").GetBoolean(), o.GetProperty("ajaxData").GetString()));
        Assert.Equal("executionId,startedAt,repName,target,kind,flags,durationSeconds,exceptionId,executionId", o.GetProperty("columns").GetString());

        // the lookups: the periods (the baseline = the last 7 days), the 14 REAL flags (never "QR not scanned")
        var lookups = s.GetProperty("lookups");
        var flagItems = lookups.GetProperty("filterFlags").GetProperty("items").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(14, flagItems.Count);
        Assert.DoesNotContain(flagItems, x => x.StartsWith("qr_missing", StringComparison.Ordinal));
        Assert.Contains("location_far=" + tr["Flag_location_far"], flagItems);
        Assert.Equal(["last30=" + tr["PeriodLast30"], "month=Ekim 2026", "custom=" + tr["PeriodCustom"]], lookups.GetProperty("filterPeriod").GetProperty("items").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(tr["AccountAll"], lookups.GetProperty("filterAccount").GetProperty("showAll").GetString());

        // the cells: neutral chips with their values, "QR not scanned" (not a flag), the exception link, the row actions
        var cells = s.GetProperty("cells").EnumerateArray().ToArray();
        Assert.Contains(Encode(tr["Flag_location_far"]) + " · 820 m", cells[1].GetProperty("flags").GetString());
        Assert.Contains(Encode(tr["Flag_short_visit"]) + " · " + Encode(Fmt(tr["MinSec"], 1, 40)), cells[3].GetProperty("flags").GetString());
        Assert.Contains(Encode(tr["Chip_qr_missing"]), cells[4].GetProperty("flags").GetString());
        Assert.Contains("vf-chip-info", cells[4].GetProperty("flags").GetString());
        Assert.Equal("<a class=\"vf-link\" href=\"/CRM/VisitEvidenceSettings?exception=exc-1\">" + Encode(tr["ExceptionLink"]) + "</a>", cells[4].GetProperty("exception").GetString());
        Assert.Contains("<bdi>Sema Kara</bdi>", cells[0].GetProperty("rep").GetString());
        Assert.Contains("<bdi>Dr. Hakan Öztürk</bdi>", cells[0].GetProperty("target").GetString());
        Assert.Equal("[quickView][open]", cells[0].GetProperty("actions").GetString());
        Assert.True(s.GetProperty("authHeaders").GetBoolean());                                                // every call carries the session headers

        // the rep summary: client mode, built on the first answer, the SAME filter state; no Save View / filter of its own
        var repTable = s.GetProperty("repTable");
        Assert.Equal((1, "client", "/CRM/VisitEvidenceFlags/api/summary?", false, false), (repTable.GetProperty("count").GetInt32(), repTable.GetProperty("dataMode").GetString(),
            repTable.GetProperty("url").GetString(), repTable.GetProperty("savedView").GetBoolean(), repTable.GetProperty("filters").GetBoolean()));
        Assert.Equal(2, s.GetProperty("repRows").GetInt32());
        var card = s.GetProperty("card");
        Assert.Equal(("412", "37"), (card.GetProperty("started").GetString(), card.GetProperty("flagged").GetString()));
        Assert.Contains("9,0", card.GetProperty("rate").GetString());
        Assert.Contains(Encode(Fmt(tr["SumOther"], 4)), card.GetProperty("bars").GetString());
        Assert.Equal(["=" + Fmt(tr["RepAllTeam"], 2), "rep-1=⁨Sema Kara⁩", "rep-2=⁨Murat Demir⁩"], s.GetProperty("repOptions").EnumerateArray().Select(x => x.GetString()));
        var repCells = s.GetProperty("repCells");
        Assert.Contains("<bdi>Murat Demir</bdi>", repCells.GetProperty("rep").GetString());
        Assert.Contains("11,0", repCells.GetProperty("rate").GetString());
        Assert.Contains(Encode(tr["Flag_location_far"]), repCells.GetProperty("top").GetString());
        Assert.Equal("[filterRep]", repCells.GetProperty("action").GetString());

        // Save View keeps ONE state: a saved / applied state is read by the card + the rep table; the same state reads once;
        // only the period (custom from / to), the rep — never a flag, an institution, the scope or a page size
        Assert.Equal(new[] { "/CRM/VisitEvidenceFlags/api/summary?period=last30&resourceId=rep-2", "/CRM/VisitEvidenceFlags/api/summary?period=custom&from=2026-09-01&to=2026-09-30" },
            s.GetProperty("summaryLoads").EnumerateArray().Select(x => x.GetString()));

        // M4 — the standard table's empty row carries the page's empty state; its title follows "only flagged"
        Assert.Contains(Encode(tr["EmptyTitle"]), s.GetProperty("emptyFlagged").GetString());
        Assert.Contains(Encode(tr["EmptyAllTitle"]), s.GetProperty("emptyAll").GetString());
        Assert.Contains("bx-check-shield", s.GetProperty("emptyFlagged").GetString());
        Assert.Equal("untouched", s.GetProperty("emptyWithRows").GetString());

        // the 92-day Apply guard: 120 and 93 days stop BEFORE the factory's Apply (the standard warning), 92 passes; a preset
        // clears a stale custom range and hides the date fields
        Assert.True(s.GetProperty("rangeShownForCustom").GetBoolean());
        var longRange = s.GetProperty("guardLong");
        Assert.Equal((0, true, true), (longRange.GetProperty("applies").GetInt32(), longRange.GetProperty("stopped").GetBoolean(), longRange.GetProperty("invalid").GetBoolean()));
        Assert.Equal(Fmt(tr["ErrRangeLong"], 92), longRange.GetProperty("toast")[0].GetString());
        Assert.Equal("warning", longRange.GetProperty("toast")[1].GetString());
        Assert.Equal(0, s.GetProperty("guard93").GetProperty("applies").GetInt32());
        Assert.Equal((1, 0, false), (s.GetProperty("guard92").GetProperty("applies").GetInt32(), s.GetProperty("guard92").GetProperty("toasts").GetInt32(), s.GetProperty("guard92").GetProperty("invalid").GetBoolean()));
        var preset = s.GetProperty("guardPreset");
        Assert.Equal((2, "", "", true), (preset.GetProperty("applies").GetInt32(), preset.GetProperty("from").GetString(), preset.GetProperty("to").GetString(), preset.GetProperty("hidden").GetBoolean()));

        // "only flagged": the switch writes the hidden scope, a restored scope moves the switch
        Assert.Equal("all", s.GetProperty("scopeFromSwitch").GetString());
        Assert.True(s.GetProperty("switchFromScope").GetBoolean());

        // M3 — a rep (the row's button or a click on the row) filters the visits through the standard Apply, the visits tab shows
        var repAction = s.GetProperty("repAction");
        Assert.Equal(("rep-9", 1, "vf-tab-visits", true), (repAction.GetProperty("value").GetString(), repAction.GetProperty("applies").GetInt32(), repAction.GetProperty("tab").GetString(), repAction.GetProperty("added").GetBoolean()));
        Assert.Equal(("rep-2", 2), (s.GetProperty("repRowClick").GetProperty("value").GetString(), s.GetProperty("repRowClick").GetProperty("applies").GetInt32()));

        // M2 — the quick view: the row at once, then the detail; every section; no photo key → "photo present, not allowed"
        Assert.DoesNotContain("bx-shield-quarter\"></i> " + Encode(tr["SecException"]) + "</div>", s.GetProperty("quickFirst").GetString());              // the exception arrives with the detail
        var quick = s.GetProperty("quick");
        var body = quick.GetProperty("body").GetString()!;
        foreach (var key in new[] { "SecLocation", "LocDistance", "LocAccuracy", "LocRadius", "SecQr", "QrResult", "QrSt_missing_exception", "SecDevice", "DevPlatform", "Plat_web",
                     "DevVerdict", "Verdict_web", "SecDuration", "DurVisit", "DurSlides", "SecPhoto", "PhotoNoPermission", "XRule_qr_optional", "ExOpen" })
        {
            Assert.Contains(Encode(tr[key]), body);
        }

        Assert.Contains("bx-shield-quarter\"></i> " + Encode(tr["SecException"]) + "</div>", body);              // the exception section, with the detail

        Assert.Contains(Encode(Fmt(tr["SlidesValue"], Fmt(tr["MinSec"], 11, 50), 8)), body);                      // the exact slide time
        Assert.DoesNotContain("<img", body);
        Assert.Contains("<bdi>Sema Kara</bdi>", quick.GetProperty("sub").GetString());
        Assert.Equal("/CRM/VisitReport?plannedVisitId=pv-e7", quick.GetProperty("open").GetString());
        Assert.Contains(Encode(tr["Flag_location_far"]) + " · " + Encode(Fmt(tr["UnitKm"], "198,3")), s.GetProperty("quickFar").GetString());
        Assert.Equal(new[] { "/CRM/VisitReport?plannedVisitId=pv-e2" }, s.GetProperty("assigned").EnumerateArray().Select(x => x.GetString()));
        Assert.All(s.GetProperty("calls").EnumerateArray(), u => Assert.DoesNotContain("photo", u.GetString()!));
    }

    [Fact]
    public void S7_tenant_wide_with_the_photo_keys_all_reps_and_the_thumbnail_and_in_arabic_the_words_and_isolated_names()
    {
        var all = FlagsSmoke("all");
        if (all is null) { return; }
        var tr = FlagsResx("tr");
        Assert.Equal(Fmt(tr["RepAllTenant"], 2), all.Value.GetProperty("repOptions")[0].GetString()!.TrimStart('='));
        var body = all.Value.GetProperty("quick").GetProperty("body").GetString()!;
        Assert.Contains("<img class=\"vf-photo\"", body);
        Assert.Contains("src=\"/CRM/VisitEvidenceFlags/api/flags/e7/photo\"", body);
        Assert.DoesNotContain(Encode(tr["PhotoNoPermission"]), body);

        // M6 — Arabic: the chips and the empty state in Arabic, every person / institution name isolated for right-to-left
        var ar = FlagsSmoke("ar")!.Value;
        var arWords = FlagsResx("ar");
        Assert.Contains(Encode(arWords["Flag_device_unverified"]), ar.GetProperty("cells")[0].GetProperty("flags").GetString());
        Assert.Contains(Encode(arWords["EmptyTitle"]), ar.GetProperty("emptyFlagged").GetString());
        Assert.Contains("<bdi>Sema Kara</bdi>", ar.GetProperty("cells")[0].GetProperty("rep").GetString());
        Assert.Contains("<bdi>Kardiyoloji Polikliniği</bdi>", ar.GetProperty("cells")[0].GetProperty("target").GetString());
        Assert.Matches("[؀-ۿ]", ar.GetProperty("lookups").GetProperty("filterFlags").GetProperty("items")[0].GetString()!);
    }

    [Fact]
    public void S8_every_word_the_scripts_name_through_T_is_a_declared_script_key()
    {
        foreach (var script in new[] { "index.js", "rep-summary.js" })
        {
            foreach (Match m in Regex.Matches(FlagsScript(script), @"\bT\('([A-Za-z0-9_]+)'"))
            {
                Assert.Contains(m.Groups[1].Value, VisitEvidenceFlagsIndex.ScriptKeys);
            }
        }

        // the bridge merges the payload into window.L10n (PascalCase), and the page reads window.L10n — never the old global
        var bridge = FlagsScript("index.l10n.js");
        Assert.Contains("document.getElementById('visitevidenceflags-l10n')", bridge);
        Assert.Contains("window.L10n = Object.assign({}, window.L10n || {}, normalized);", bridge);
        Assert.Contains("<script id=\"visitevidenceflags-l10n\" type=\"application/json\">", FlagsPartial("_IndexL10n.cshtml"));
        Assert.DoesNotContain("VisitEvidenceFlagsL10n", FlagsScript("index.js"));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static (StdGateway Gateway, VisitEvidenceFlagsController Controller) ArrangeStd(string[] permissions, Func<string, string> answer, string? acceptLanguage = null)
    {
        var gateway = new StdGateway(answer);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitEvidenceFlagsController(new HttpClient(gateway), configuration, NullLogger<VisitEvidenceFlagsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        services.AddSingleton<TimeProvider>(new PinnedClock(StdToday));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private sealed class PinnedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StdGateway(Func<string, string> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Paths() => Requests.Select(r => r[GatewayUrl.Length..]).ToList();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            Requests.Add(uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(uri[GatewayUrl.Length..]), Encoding.UTF8, "application/json") });
        }
    }
}
