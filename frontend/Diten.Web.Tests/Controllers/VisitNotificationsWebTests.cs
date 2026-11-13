using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitNotifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W5 · W5-WEB — the sent visit e-mails page (mockup 06-epostalar) and the notification settings card, on the REAL
/// views, proxy, resx and scripts: (1) the Golden Compact list markup (server mode, read only, the standard filter / header /
/// L10n bridge / quick view) and the settings card + its script rendered only for the settings key (UAS-001); (2) the proxy:
/// the list contract → the CRM query (period on a pinned clock, event KEY → the catalogue code with the pre-W5-FIX code as a
/// fallback, status, rep, refusals before any call), each row's names + recipient spec from its detail, the preview rebuilt
/// from allow-lists (no address passes), the settings GET / PUT behind the settings key (a hidden button's write checked
/// HERE), the export; (3) the page in a fake factory (Node): the cells, both event code forms under one name, the badges,
/// the recipient roles, the e-mail preview without any address, the settings card (validation, the version, 409 in words);
/// (4) every word in seven languages, the menu label, the gateway routes the proxy calls.
/// </summary>
public sealed class VisitNotificationsWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c5e1b2-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Today = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private const string Read = VisitNotificationsController.Read;
    private const string Manage = VisitNotificationsController.SettingsManage;
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Regex Address = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");

    // ═══ 1 · the markup ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void N1_the_list_is_the_shared_v2_shell_in_server_mode_read_only_with_the_standard_filter_header_and_quick_view()
    {
        var dataTable = Partial("_DataTable.cshtml");
        Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=\"listShell\" />", dataTable);
        Assert.Contains("TableId = \"dt-visitnotifications\"", dataTable);
        Assert.Contains("DataMode = \"server\"", dataTable);
        Assert.Contains("HasSelection = false", dataTable);

        var view = Partial("Index.cshtml");
        foreach (var marker in new[] { "<partial name=\"~/Views/CRM/VisitNotifications/_Filter.cshtml\" model=\"Model\" />", "<partial name=\"~/Views/CRM/VisitNotifications/_DataTable.cshtml\" />",
                     "<partial name=\"~/Views/CRM/VisitNotifications/_IndexL10n.cshtml\" />", "id=\"offcanvasDetailsPreview\"", "Layout = \"_LayoutTenantShell\";",
                     "<h5 class=\"mb-0\">@Localizer[\"PageTitle\"]</h5>", "<p class=\"mb-0 text-muted\">@Localizer[\"PageDescription\"]</p>",
                     "~/assets/js/CRM/VisitNotifications/index.l10n.js", "~/assets/js/CRM/VisitNotifications/notifications-core.js", "~/assets/js/CRM/VisitNotifications/index.js" })
        {
            Assert.Contains(marker, view);
        }

        Assert.DoesNotContain("_BulkActionBar", view);
        Assert.DoesNotContain("style=", view);                                                                  // the drawer width is the page CSS's
        // UAS-001: the notice and NOTHING else — the guard is the first thing the page does
        var denied = view.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal);
        Assert.True(denied > 0 && denied < view.IndexOf("_Filter.cshtml\" model", StringComparison.Ordinal));
        Assert.Matches(@"@if \(!Model\.CanRead\)\s*\{\s*<partial name=""_AccessDenied"" model=""@Localizer\[""PageTitle""\]\.Value"" />\s*return;\s*\}", view);

        var filter = Partial("_Filter.cshtml");
        Assert.Contains("id=\"inlineFilterCollapse\"", filter);
        foreach (var chip in new[] { "filterPeriod", "filterEvent", "filterStatus", "filterRep" })
        {
            Assert.Matches("<select id=\"" + chip + "\" class=\"form-select form-select-sm select2\" data-placeholder=", filter);
        }

        // the code: server mode, the export, no page-written serverSide / ajax.data / ext.search / page size
        var js = Script("index.js");
        Assert.Contains("list = await window.DitenDataTable.createList({", js);
        Assert.Contains("const endpoint = '/CRM/VisitNotifications/api';", js);
        Assert.Contains("export: { mode: 'server', url: endpoint + '/list/export'", js);
        var code = Regex.Replace(Regex.Replace(js, @"/\*[\s\S]*?\*/", ""), @"(^|[^:])//.*$", "$1", RegexOptions.Multiline);
        Assert.DoesNotContain("serverSide", code);
        Assert.DoesNotContain("ext.search", code);
        Assert.DoesNotMatch(@"pageSize=\d", code);

        // the layout primary, never a fixed colour (06 draws navy / green / red — the theme tokens draw them here)
        var css = Script("visit-notifications.css");
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", Regex.Replace(css, @"/\*[\s\S]*?\*/", ""));
        Assert.DoesNotMatch(@"rgba?\(\s*\d", css);
        Assert.Contains("var(--bs-primary)", css);
    }

    [Fact]
    public void N2_the_settings_tab_card_and_script_exist_only_for_the_settings_key()
    {
        // the controller decides from the keys: read alone → no settings; the settings key → the card
        Assert.Equal((true, false), Flags(Model(Arrange([Read]).Controller.Index())));
        Assert.Equal((true, true), Flags(Model(Arrange([Manage]).Controller.Index())));
        Assert.Equal((true, true), Flags(Model(Arrange([Read, Manage]).Controller.Index())));
        Assert.Equal((false, false), Flags(Model(Arrange([]).Controller.Index())));
        Assert.Equal((false, false), Flags(Model(Arrange(["crm.visit-notifications.settings.read", "crm.visit-evidence-policy.manage"]).Controller.Index())));

        // the view renders the tab card, the settings pane and the settings script ONLY inside @if (Model.CanManageSettings)
        var view = Partial("Index.cshtml");
        foreach (var piece in new[] { "<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"tabCard\" />",
                     "<partial name=\"~/Views/CRM/VisitNotifications/_SettingsCard.cshtml\" />", "~/assets/js/CRM/VisitNotifications/settings.js" })
        {
            var at = view.IndexOf(piece, StringComparison.Ordinal);
            Assert.True(at > 0, piece);
            Assert.Equal(1, Regex.Matches(view, Regex.Escape(piece)).Count);
            var guard = view.LastIndexOf("@if (Model.CanManageSettings)", at, StringComparison.Ordinal);
            Assert.True(guard > 0, piece + " is not inside the settings guard");
            Assert.DoesNotContain("}", view[(view.IndexOf('{', guard) + 1)..at]);                      // still inside that block
        }

        Assert.Equal(3, Regex.Matches(view, Regex.Escape("@if (Model.CanManageSettings)")).Count);
        var card = Partial("_SettingsCard.cshtml");
        foreach (var id in new[] { "vn-pv-email", "vn-msl-email", "vn-report-summary", "vn-overdue-reminders", "vn-settings-save", "vn-settings-alert" })
        {
            Assert.Contains("id=\"" + id + "\"", card);
        }
    }

    // ═══ 2 · the proxy ════════════════════════════════════════════════════════════════════════════════════════

    private const string Row1 = "11111111-1111-1111-1111-111111111111";
    private const string Row2 = "22222222-2222-2222-2222-222222222222";

    private static string Page(long total) => "{\"data\":{\"items\":[{\"id\":\"" + Row1 + "\",\"eventCode\":\"crm.visit-report.submitted\",\"status\":\"sent\",\"resourceId\":\"rep-1\",\"resourceName\":\"Sema Kara\",\"notes\":[]},"
        + "{\"id\":\"" + Row2 + "\",\"eventCode\":\"crm.visitreport.adverseevent\",\"status\":\"route-pending\",\"resourceId\":\"rep-2\",\"notes\":[\"pv-route-pending\"]}],\"total\":" + total + ",\"page\":1,\"pageSize\":10}}";

    private const string Detail1 = "{\"data\":{\"row\":{\"id\":\"" + Row1 + "\",\"eventCode\":\"crm.visit-report.submitted\",\"status\":\"sent\",\"resourceId\":\"rep-1\",\"email\":\"leak@x.com\"},"
        + "\"recipients\":[{\"role\":\"to\",\"kind\":\"line-manager\",\"userId\":\"rep-1\",\"mailbox\":null,\"email\":\"mgr@firma.com\"}],"
        + "\"resolved\":[{\"role\":\"to\",\"userId\":\"mgr-1\",\"via\":\"line-manager\",\"address\":\"mgr@firma.com\"}],"
        + "\"variables\":{\"repName\":\"Sema Kara\",\"doctorName\":\"Dr. Hakan Öztürk\",\"institutionName\":\"Kardiyoloji\",\"visitDate\":\"2026-10-08\",\"replyTo\":\"rep@firma.com\",\"pvEmail\":\"pv@firma.com\"},"
        + "\"lastError\":\"550 mailbox mgr@firma.com unavailable\"}}";

    [Fact]
    public async Task N3_the_proxy_turns_the_list_contract_into_the_crm_query_and_adds_each_rows_names_and_recipient_spec()
    {
        // the baseline: the last 30 days on the pinned clock, start 20 / length 10 → page 3, each row's detail read
        var (gw, c) = Arrange([Read], path => path.Contains("/" + Row1, StringComparison.Ordinal) ? Detail1 : path.Contains("/" + Row2, StringComparison.Ordinal) ? "{}" : Page(37));
        var ok = Assert.IsType<OkObjectResult>(await c.List(20, 10, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        var paths = gw.Paths();
        Assert.Equal("/api/crm/visit-notifications?from=2026-09-21&to=2026-10-20&page=3&pageSize=10", paths[0]);
        Assert.Equal(new[] { "/api/crm/visit-notifications/" + Row1, "/api/crm/visit-notifications/" + Row2 }, paths.Skip(1).Order());
        var body = Json(ok.Value).GetProperty("data");
        Assert.Equal((37, 37), (body.GetProperty("total").GetInt64(), body.GetProperty("filteredTotal").GetInt64()));
        var first = body.GetProperty("items")[0];
        Assert.Equal(("Dr. Hakan Öztürk", "Kardiyoloji"), (first.GetProperty("target").GetProperty("doctorName").GetString(), first.GetProperty("target").GetProperty("institutionName").GetString()));
        Assert.Equal("line-manager", first.GetProperty("recipients")[0].GetProperty("kind").GetString());
        Assert.False(body.GetProperty("items")[1].TryGetProperty("target", out _));                          // an unreadable detail leaves the row as it came
        Assert.DoesNotMatch(Address, body.GetRawText());                                                         // the allow-list: no address passes

        // every filter: the event KEY → the catalogue code, the status, the rep; a narrowed list asks the whole total once
        var (gw2, c2) = Arrange([Manage], path => path.Contains("pageSize=1", StringComparison.Ordinal) && !path.Contains("eventCode", StringComparison.Ordinal) ? Page(412) : Page(7));
        var narrowed = Assert.IsType<OkObjectResult>(await c2.List(0, 25, null, null, null, "custom", "2026-09-01", "2026-09-30", "adverseevent", "route-pending", "rep-2", CancellationToken.None));
        Assert.Equal("/api/crm/visit-notifications?from=2026-09-01&to=2026-09-30&eventCode=crm.visitreport.adverseevent&status=route-pending&resourceId=rep-2&page=1&pageSize=25", gw2.Paths()[0]);
        Assert.Contains("/api/crm/visit-notifications?from=2026-09-01&to=2026-09-30&page=1&pageSize=1", gw2.Paths());
        Assert.Equal((412, 7), (Json(narrowed.Value).GetProperty("data").GetProperty("total").GetInt64(), Json(narrowed.Value).GetProperty("data").GetProperty("filteredTotal").GetInt64()));

        // the CRM still on its pre-W5-FIX constants refuses the catalogue code → ONE retry with the old hyphenated code
        var (gw3, c3) = Arrange([Read], path => path.Contains("eventCode=crm.visitreport.", StringComparison.Ordinal) ? "!400" : Page(1));
        Assert.IsType<OkObjectResult>(await c3.List(0, 10, null, null, null, "last7", null, null, "offlabelrequest", null, null, CancellationToken.None));
        Assert.Equal("/api/crm/visit-notifications?from=2026-10-14&to=2026-10-20&eventCode=crm.visitreport.offlabelrequest&page=1&pageSize=10", gw3.Paths()[0]);
        Assert.Equal("/api/crm/visit-notifications?from=2026-10-14&to=2026-10-20&eventCode=crm.visit-report.off-label-request&page=1&pageSize=10", gw3.Paths()[1]);

        // refusals BEFORE any CRM call; without a read key 403 and no call
        foreach (var refused in new Func<VisitNotificationsController, Task<IActionResult>>[]
                 {
                     x => x.List(0, 101, null, null, null, null, null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 10, "Kara", null, null, null, null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 10, null, "createdAt", null, null, null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, null, null, null, "crm.visit-report.submitted", null, null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, null, null, null, null, "delivered", null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, "custom", "2026-10-10", "2026-10-01", null, null, null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, "quarter", null, null, null, null, null, CancellationToken.None)
                 })
        {
            var (g, ctl) = Arrange([Read], _ => Page(1));
            Assert.Equal(400, Status(await refused(ctl)));
            Assert.Empty(g.Requests);
        }

        var (gw4, c4) = Arrange(["crm.visit-evidence-flags.read"], _ => Page(1));
        Assert.Equal(403, Status(await c4.List(0, 10, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(403, Status(await c4.Detail(Guid.Parse(Row1), CancellationToken.None)));
        Assert.Empty(gw4.Requests);
    }

    [Theory]
    [InlineData(null, null, null, "2026-09-21", "2026-10-20")]
    [InlineData("last30", null, null, "2026-09-21", "2026-10-20")]
    [InlineData("LAST7", null, null, "2026-10-14", "2026-10-20")]
    [InlineData("month", null, null, "2026-10-01", "2026-10-20")]
    [InlineData("custom", "2025-01-01", "2026-10-20", "2025-01-01", "2026-10-20")]
    [InlineData("custom", "2026-10-21", "2026-10-20", null, null)]
    [InlineData("custom", "20.10.2026", "2026-10-20", null, null)]
    [InlineData("custom", null, "2026-10-20", null, null)]
    [InlineData("year", null, null, null, null)]
    public void N4_the_period_window_is_the_twin_of_the_page_rule(string? period, string? from, string? to, string? wantFrom, string? wantTo)
    {
        var r = VisitNotificationsController.ResolveRange(period, from, to, DateOnly.FromDateTime(Today.UtcDateTime));
        Assert.Equal((wantFrom, wantTo), (r.From, r.To));
        Assert.Equal(wantFrom is null, r.Error is not null);

        // the page's own rule says the same (notifications-core.js rangeOf)
        var node = RunNode(Core() + "const r = C.rangeOf(" + JsArg(period ?? "") + ", '2026-10-20', " + JsArg(from) + ", " + JsArg(to) + "); process.stdout.write(JSON.stringify(r));");
        if (node is null) return;
        var js = JsonDocument.Parse(node).RootElement;
        Assert.Equal(wantFrom, js.TryGetProperty("from", out var f) ? f.GetString() : null);
        Assert.Equal(wantTo, js.TryGetProperty("to", out var t) ? t.GetString() : null);
    }

    [Fact]
    public async Task N5_the_preview_is_rebuilt_from_allow_lists_no_address_and_no_last_error_ever_passes()
    {
        var (gw, c) = Arrange([Read], _ => Detail1);
        var ok = Assert.IsType<OkObjectResult>(await c.Detail(Guid.Parse(Row1), CancellationToken.None));
        Assert.Equal(new[] { "/api/crm/visit-notifications/" + Row1 }, gw.Paths());
        var data = Json(ok.Value).GetProperty("data");
        Assert.DoesNotMatch(Address, data.GetRawText());
        Assert.False(data.TryGetProperty("lastError", out _));
        Assert.Equal(new[] { "repName", "doctorName", "institutionName", "visitDate" }, data.GetProperty("variables").EnumerateObject().Select(p => p.Name));
        Assert.Equal("line-manager", data.GetProperty("recipients")[0].GetProperty("kind").GetString());
        Assert.Equal("crm.visit-report.submitted", data.GetProperty("row").GetProperty("eventCode").GetString());

        // the CRM's own refusal (404 outside the scope) is passed as it came
        var (_, c2) = Arrange([Read], _ => "!404");
        Assert.Equal(404, Status(await c2.Detail(Guid.Parse(Row1), CancellationToken.None)));
    }

    [Fact]
    public async Task N6_the_settings_are_read_and_written_only_with_the_settings_key_checked_in_the_proxy_itself()
    {
        const string Settings = "{\"data\":{\"pvEmail\":\"pv@firma.com\",\"mslEmail\":null,\"overdueReminders\":true,\"reportSummary\":true,\"version\":2,\"isDefault\":false}}";

        // read key only: neither the GET nor the PUT reaches the CRM (the card is not drawn — and the endpoint does not trust that)
        var (gw, c) = Arrange([Read], _ => Settings);
        Assert.Equal(403, Status(await c.GetSettings(CancellationToken.None)));
        c.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"pvEmail\":\"x@y.com\",\"expectedVersion\":2}"));
        Assert.Equal(403, Status(await c.PutSettings(CancellationToken.None)));
        Assert.Empty(gw.Requests);

        // the settings key: GET and PUT pass, the body as sent (no tenant in it), the method kept
        var (gw2, c2) = Arrange([Manage], _ => Settings);
        Assert.Equal(200, Status(await c2.GetSettings(CancellationToken.None)));
        c2.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"pvEmail\":\"yeni@firma.com\",\"mslEmail\":\"\",\"reportSummary\":true,\"overdueReminders\":false,\"expectedVersion\":2}"));
        Assert.Equal(200, Status(await c2.PutSettings(CancellationToken.None)));
        Assert.Equal(new[] { "GET /api/crm/visit-notifications/settings", "PUT /api/crm/visit-notifications/settings" }, gw2.Calls.Select(x => x.Method + " " + x.Path));
        Assert.Contains("\"expectedVersion\":2", gw2.Calls[1].Body);

        // a body naming the tenant is refused before the CRM; the CRM's 409 reaches the page as 409
        var (gw3, c3) = Arrange([Manage], _ => Settings);
        c3.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"tenantId\":\"" + Guid.NewGuid() + "\",\"pvEmail\":\"a@b.com\"}"));
        Assert.Equal(400, Status(await c3.PutSettings(CancellationToken.None)));
        Assert.Empty(gw3.Requests);
        var (_, c4) = Arrange([Manage], _ => "!409");
        c4.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"expectedVersion\":1}"));
        Assert.Equal(409, Status(await c4.PutSettings(CancellationToken.None)));
    }

    [Fact]
    public async Task N7_the_export_writes_every_match_in_the_readers_words_and_the_roles_never_an_address()
    {
        string Rows(int from, int count, int total) => "{\"data\":{\"items\":[" + string.Join(",", Enumerable.Range(from, count).Select(i =>
            "{\"id\":\"" + Guid.NewGuid() + "\",\"eventCode\":\"" + (i % 2 == 0 ? "crm.visit-report.adverse-event" : "crm.visitreport.submitted") + "\",\"status\":\"" + (i % 2 == 0 ? "route-pending" : "sent")
            + "\",\"resourceId\":\"rep-1\",\"resourceName\":\"Sema Kara\",\"createdAt\":\"2026-10-08T08:58:00Z\",\"notes\":[" + (i % 2 == 0 ? "\"pv-route-pending\"" : "") + "]}")) + "],\"total\":" + total + "}}";
        var (gw, c) = Arrange([Read], path => path.Contains("page=1&", StringComparison.Ordinal) ? Rows(0, 100, 101) : Rows(100, 1, 101), "tr");
        var file = Assert.IsType<FileContentResult>(await c.Export("csv", ["createdAt", "eventCode", "resourceName", "recipients", "status"], null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Equal(2, gw.Requests.Count);                                                                    // 100 + 1, never a row's detail
        var csv = Encoding.UTF8.GetString(file.FileContents);
        var tr = Resx("tr");
        Assert.Contains(tr["Event_adverseevent"], csv);                                                        // the old code under the event's name
        Assert.Contains(tr["Event_submitted"], csv);
        Assert.Contains(tr["St_route_pending_pv"], csv);
        Assert.DoesNotMatch(Address, csv);
        Assert.Equal(102, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);                       // the header + every row

        Assert.Equal(["Rcp_pv", "Rcp_manager"], VisitNotificationsController.RoleKeysOf("adverseevent", "sent", []));
        Assert.Equal(["Rcp_manager"], VisitNotificationsController.RoleKeysOf("adverseevent", "route-pending", ["pv-route-pending"]));
        Assert.Equal(["Rcp_msl", "Rcp_manager"], VisitNotificationsController.RoleKeysOf("offlabelrequest", "sent", []));
        Assert.Equal(["RcpNone"], VisitNotificationsController.RoleKeysOf("submitted", "no-recipient", []));
        Assert.Equal(403, Status(await Arrange([], _ => "{}").Controller.Export("csv", null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
    }

    [Theory]
    [InlineData("crm.visitreport.submitted", "submitted")]
    [InlineData("crm.visit-report.submitted", "submitted")]
    [InlineData("crm.visitreport.adverseevent", "adverseevent")]
    [InlineData("crm.visit-report.adverse-event", "adverseevent")]
    [InlineData("crm.visitreport.offlabelrequest", "offlabelrequest")]
    [InlineData("crm.visit-report.off-label-request", "offlabelrequest")]
    [InlineData("CRM.VISIT-REPORT.OVERDUE", "overdue")]
    [InlineData("crm.visitreport.order", null)]
    [InlineData("crm.meeting.organizer-added", null)]
    [InlineData(null, null)]
    public void N8_both_forms_of_an_event_code_name_the_same_event_in_the_proxy_and_the_page(string? code, string? key)
    {
        Assert.Equal(key, VisitNotificationsController.EventKeyOf(code));
        var node = RunNode(Core() + "process.stdout.write(JSON.stringify(C.eventKey(" + JsArg(code) + ")));");
        if (node is null) return;
        Assert.Equal(key is null ? "null" : "\"" + key + "\"", node);
        if (key is not null) Assert.Equal(VisitNotificationsController.EventKeys[key].Code, "crm.visitreport." + key);
    }

    // ═══ 3 · the page in a fake factory (Node) ════════════════════════════════════════════════════════════════

    [Fact]
    public void N9_the_page_cells_badges_roles_and_the_email_preview_without_any_address()
    {
        var r = Smoke("manager");
        if (r is null) return;
        var s = r.Value;
        var tr = Resx("tr");

        var o = s.GetProperty("options");
        Assert.Equal(("server", "/CRM/VisitNotifications/api/list", "server", "/CRM/VisitNotifications/api/list/export"),
            (o.GetProperty("dataMode").GetString(), o.GetProperty("ajax").GetString(), o.GetProperty("exportMode").GetString(), o.GetProperty("exportUrl").GetString()));
        Assert.Equal("filterPeriod:period:single,vn-from:from:text,vn-to:to:text,filterEvent:event:single,filterStatus:status:single,filterRep:resourceId:single", o.GetProperty("fields").GetString());
        Assert.Equal("id,createdAt,eventCode,target,resourceName,recipients,status,id", o.GetProperty("columns").GetString());
        Assert.Equal(("CRM/VisitNotifications", "[1,2,3,4,5,6]", "[1,2,4,5,6]"), (o.GetProperty("savedView").GetString(), o.GetProperty("saveViewColumns").GetString(), o.GetProperty("exportColumns").GetString()));
        Assert.False(o.GetProperty("anySortable").GetBoolean());
        Assert.False(o.GetProperty("searching").GetBoolean());
        Assert.Equal(("[]", false, "undefined"), (o.GetProperty("order").GetString(), o.GetProperty("serverSideWritten").GetBoolean(), o.GetProperty("ajaxData").GetString()));

        // the lookups: the baseline = the last 30 days; the four events by KEY; the five statuses
        var lookups = s.GetProperty("lookups");
        Assert.Equal(tr["PeriodLast30"], lookups.GetProperty("filterPeriod").GetProperty("showAll").GetString());
        Assert.Equal(["submitted=" + tr["Event_submitted"], "adverseevent=" + tr["Event_adverseevent"], "offlabelrequest=" + tr["Event_offlabelrequest"], "overdue=" + tr["Event_overdue"]],
            lookups.GetProperty("filterEvent").GetProperty("items").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(["sent", "queued", "failed", "no-recipient", "route-pending"], lookups.GetProperty("filterStatus").GetProperty("items").EnumerateArray().Select(x => x.GetString()!.Split('=')[0]));

        // the cells: the old hyphenated and the catalogue code under the SAME name; the badges; the roles, never an address
        var cells = s.GetProperty("cells").EnumerateArray().ToArray();
        Assert.Equal(tr["Event_adverseevent"], cells[1].GetProperty("eventText").GetString());               // crm.visit-report.adverse-event
        Assert.Equal(tr["Event_offlabelrequest"], cells[2].GetProperty("eventText").GetString());            // crm.visitreport.offlabelrequest
        Assert.Equal(tr["Event_submitted"], cells[0].GetProperty("eventText").GetString());
        Assert.Equal(tr["St_route_pending_pv"], cells[1].GetProperty("statusText").GetString());
        Assert.Contains("bg-label-info", cells[1].GetProperty("status").GetString());
        Assert.Equal(tr["St_failed_final"], cells[3].GetProperty("statusText").GetString());
        Assert.Equal(tr["St_no_recipient"], cells[4].GetProperty("statusText").GetString());
        // W5-FIX2 (F-6) — "sent" reads İletildi (handed over to the e-mail service), said on hover too
        Assert.Equal(("İletildi", "E-posta servisine teslim edildi."), (tr["St_sent"], tr["Explain_sent"]));
        Assert.Equal(tr["St_sent"], cells[0].GetProperty("statusText").GetString());
        Assert.Contains("title=\"" + Encode(tr["Explain_sent"]) + "\"", cells[0].GetProperty("status").GetString());
        Assert.Equal(tr["Rcp_upper_manager"], cells[1].GetProperty("recipientsText").GetString());           // PV not set → the manager (found up the chain) moved up
        Assert.Contains(Encode(Fmt(tr["RcpPending"], tr["Rcp_pv"])), cells[1].GetProperty("recipients").GetString());
        Assert.Equal(tr["Rcp_msl"] + ", " + tr["Rcp_manager"], cells[2].GetProperty("recipientsText").GetString());
        Assert.Contains(Encode(Fmt(tr["CcSuffix"], tr["Rcp_manager"])), cells[2].GetProperty("recipients").GetString());
        Assert.Equal(tr["RcpNone"], cells[4].GetProperty("recipientsText").GetString());
        Assert.Contains("<bdi>Dr. Hakan Öztürk</bdi>", cells[0].GetProperty("target").GetString());
        Assert.Equal("[quickView]", cells[0].GetProperty("actions").GetString());
        Assert.True(s.GetProperty("authHeaders").GetBoolean());

        // the preview (06): both code forms render the SAME e-mail; ACİL subject + 24 h band + the "no medical assessment"
        // note; the upper manager (found up the chain); the KVKK footer with the rep; the link only when it is safe
        var adverse = s.GetProperty("adverse").GetProperty("body").GetString()!;
        Assert.Equal(adverse, s.GetProperty("adverseHyphenless").GetProperty("body").GetString());
        // FU-W5-PREVIEW-SUBJECT — the real mail's subject: the doctor, not the products
        Assert.Contains(Encode(tr["Subj_adverseevent"]).Replace("{0}", "<bdi>Dr. Hakan Öztürk</bdi>"), adverse);
        Assert.DoesNotContain("<bdi>UROFLEKS, TUTUKON</bdi> · 24", adverse);
        Assert.Contains("vn-band-danger", adverse);
        Assert.Contains(Encode(tr["Band_adverseevent"]), adverse);
        Assert.Contains(Encode(tr["Note_adverseevent"]), adverse);
        Assert.Contains(Encode(tr["Rcp_upper_manager"]), adverse);
        Assert.Contains(Encode(tr["Explain_route_pending_pv"]), adverse);
        Assert.Contains("•••@•••", adverse);                                                                  // an address typed into the note is masked
        Assert.Contains(Encode(tr["FooterKvkk"]).Replace("{0}", "<bdi>Murat Demir</bdi>"), adverse);           // the KVKK footer, replies to the rep
        Assert.Equal("/CRM/VisitReport?plannedVisitId=pv-n2", s.GetProperty("adverse").GetProperty("open").GetString());
        var summary = s.GetProperty("summary");
        Assert.True(summary.GetProperty("openHidden").GetBoolean());                                          // a javascript: link is no link
        Assert.DoesNotContain("javascript:", summary.GetProperty("body").GetString());
        Assert.Contains(Encode(tr["Safety_offlabel_yes"]), summary.GetProperty("body").GetString());
        Assert.Contains(Encode(tr["Safety_adverse_no"]), summary.GetProperty("body").GetString());
        Assert.Contains(Encode(tr["Explain_sent"]), summary.GetProperty("body").GetString());                 // W5-FIX2 — the preview band says it too
        var overdue = s.GetProperty("overdue");
        Assert.Equal(tr["CtaReviewRecord"], overdue.GetProperty("openText").GetString());
        Assert.Contains(Encode(tr["Explain_failed_final"]), overdue.GetProperty("body").GetString());
        Assert.Contains(Encode(tr["Row_deadline"]), overdue.GetProperty("body").GetString());
        var none = s.GetProperty("noRecipient").GetProperty("body").GetString()!;
        Assert.Contains(Encode(tr["RcpNone"]), none);
        Assert.DoesNotContain(">" + Encode(tr["MailCc"]) + "<", none);                                        // nobody resolved: no role claimed

        // the settings card is loaded (its addresses are in the fields) — and STILL no address anywhere in a cell or preview
        Assert.Equal("pv.guvenlik@firma.com", s.GetProperty("settingsLoaded").GetProperty("pv").GetString());
        Assert.Equal(JsonValueKind.Null, s.GetProperty("addressInPreview").ValueKind);

        // the empty state, the custom range guard (a reversed range is never sent)
        Assert.Contains(Encode(tr["EmptyTitle"]), s.GetProperty("empty").GetString());
        Assert.True(s.GetProperty("rangeShown").GetBoolean());
        Assert.Equal(0, s.GetProperty("guardReversed").GetProperty("applies").GetInt32());
        Assert.Equal(tr["ErrRangeOrder"], s.GetProperty("guardReversed").GetProperty("toast")[0].GetString());
        Assert.Equal(1, s.GetProperty("guardOk").GetProperty("applies").GetInt32());
    }

    [Fact]
    public void N10_the_settings_card_validates_sends_the_version_read_and_says_a_409_in_words()
    {
        var r = Smoke("manager");
        if (r is null) return;
        var s = r.Value;
        var tr = Resx("tr");
        var loaded = s.GetProperty("settingsLoaded");
        Assert.Equal(("", false, true), (loaded.GetProperty("msl").GetString(), loaded.GetProperty("summary").GetBoolean(), loaded.GetProperty("overdue").GetBoolean()));
        Assert.Contains("Ayşe Yönetici", loaded.GetProperty("meta").GetString());
        Assert.DoesNotContain("c5769c62", loaded.GetProperty("meta").GetString());                          // W5-FIX2 (F-3) — the name, never the id

        var invalid = s.GetProperty("saveInvalid");
        Assert.Equal((0, true, true, tr["ErrEmailField"]), (invalid.GetProperty("puts").GetInt32(), invalid.GetProperty("pvInvalid").GetBoolean(), invalid.GetProperty("mslInvalid").GetBoolean(), invalid.GetProperty("pvMsg").GetString()));

        var saved = s.GetProperty("saveOk");
        var body = saved.GetProperty("body");
        Assert.Equal(("yeni.pv@firma.com", "", true, false, 3), (body.GetProperty("pvEmail").GetString(), body.GetProperty("mslEmail").GetString(),
            body.GetProperty("reportSummary").GetBoolean(), body.GetProperty("overdueReminders").GetBoolean(), body.GetProperty("expectedVersion").GetInt32()));
        Assert.Equal(tr["SettingsSaved"], saved.GetProperty("toast")[0].GetString());
        Assert.Equal(4, saved.GetProperty("version").GetInt32());                                             // the next save sends the new version

        var conflict = s.GetProperty("save409");
        Assert.Equal((tr["Err_conflict"], "alert alert-warning", "baska@firma.com", 4),
            (conflict.GetProperty("alert").GetString(), conflict.GetProperty("alertClass").GetString(), conflict.GetProperty("kept").GetString(), conflict.GetProperty("expected").GetInt32()));
        Assert.Equal(tr["Err_visit_notification_email_invalid"], s.GetProperty("save400").GetProperty("alert").GetString());
        Assert.True(s.GetProperty("save400").GetProperty("pvInvalid").GetBoolean());
        Assert.Equal(["GET", "PUT", "PUT", "PUT"], s.GetProperty("settingsCalls").EnumerateArray().Select(x => x.GetString()));
    }

    /// <summary>W5-FIX2 (F-2, F-6) — no internal decision code reaches the settings card, and the "sent" badge and its
    /// explanation exist in every language.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public void N14_the_settings_note_names_no_internal_decision_and_the_sent_badge_is_explained_in_each_language(string lang)
    {
        var words = Resx(lang);
        Assert.DoesNotContain("K-W8", words["MailboxEmptyNote"]);
        Assert.DoesNotMatch(@"\bK-[A-Z]*\d", words["MailboxEmptyNote"]);
        Assert.False(string.IsNullOrWhiteSpace(words["Explain_sent"]));
        Assert.False(string.IsNullOrWhiteSpace(words["St_sent"]));
        if (lang != "en")
        {
            Assert.NotEqual(Resx("en")["Explain_sent"], words["Explain_sent"]);
        }
    }

    /// <summary>W5-FIX2 (F-4) — the preview's subject leaves an empty part out with its separator (the twin of the Platform's
    /// EmailTemplateRenderer.WithoutEmptySegments): no product → no "· — ·"; a filled part, or a part with no slot, stays.</summary>
    [Fact]
    public void N15_the_preview_subject_leaves_an_empty_part_out_with_its_separator()
    {
        var tr = Resx("tr");
        string Run(string key, string args) =>
            RunNode(Core() + "process.stdout.write(C.withoutEmptySegments(" + JsArg(tr[key]) + ", " + args + "));") ?? "<no node>";
        if (RunNode("process.stdout.write('ok')") is null) return;

        Assert.Equal("ACİL · Advers olay bildirimi · 24 saat", Run("Subj_adverseevent", "['']"));
        Assert.Equal("ACİL · Advers olay bildirimi · {0} · 24 saat", Run("Subj_adverseevent", "['UROFLEKS']"));
        Assert.Equal("Endikasyon dışı bilgi talebi", Run("Subj_offlabelrequest", "[null]"));
        Assert.Equal("Ziyaret raporu · {1}", Run("Subj_submitted", "['', '20.10.2026']"));
    }

    /// <summary>FU-W5-PREVIEW-SUBJECT (user decision 2026-10-21) — the preview's subject IS the Platform template's subject, in
    /// every language: read from the seed's own source (NotificationTemplateSeed, VisitReport* methods), {{doctorName}} →
    /// {0}, {{visitDate}} → {1}. A wording changed on one side only turns this red.</summary>
    [Theory]
    [InlineData("submitted", "VisitReportSubmitted")]
    [InlineData("adverseevent", "VisitReportAdverseEvent")]
    [InlineData("offlabelrequest", "VisitReportOffLabelRequest")]
    [InlineData("overdue", "VisitReportOverdue")]
    public void N16_the_preview_subject_is_the_platform_templates_subject_in_seven_languages(string key, string method)
    {
        var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(WebRoot()))!;
        var seed = File.ReadAllText(Path.Combine(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "Persistence",
            "Configurations", "NotificationTemplateSeed.cs"));
        var start = seed.IndexOf($"private static NotificationTemplate {method}(string locale)", StringComparison.Ordinal);
        Assert.True(start >= 0, method);
        var body = seed[start..seed.IndexOf("return VisitReportTemplate(", start, StringComparison.Ordinal)];
        var platform = System.Text.RegularExpressions.Regex.Matches(body, @"""(en|tr|fr|es|zh|ar|ru)"" => \(""((?:[^""\\]|\\.)*)""")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Replace("{{doctorName}}", "{0}").Replace("{{visitDate}}", "{1}"));
        Assert.Equal(7, platform.Count);

        foreach (var (lang, subject) in platform)
        {
            Assert.Equal(subject, Resx(lang)["Subj_" + key]);
        }

        Assert.Equal(platform["en"], Resx("")["Subj_" + key]);
    }

    [Fact]
    public void N17_the_preview_subject_takes_the_doctor_and_the_raw_visit_date_never_the_products()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        string Subject(string key, string vars) =>
            RunNode(Core() + "const t = C.template(" + JsArg(key) + ", " + vars + ", { day: (x) => 'D(' + x + ')', moment: (x) => x }); process.stdout.write(JSON.stringify(t.subject));")!;

        const string vars = "{ doctorName: 'Dr. A', institutionName: 'Kurum', visitDate: '2026-10-08', products: 'URO, TUT' }";
        Assert.Equal("{\"key\":\"Subj_adverseevent\",\"args\":[\"Dr. A\"]}", Subject("adverseevent", vars));
        Assert.Equal("{\"key\":\"Subj_offlabelrequest\",\"args\":[\"Dr. A\"]}", Subject("offlabelrequest", vars));
        Assert.Equal("{\"key\":\"Subj_overdue\",\"args\":[\"Dr. A\",\"2026-10-08\"]}", Subject("overdue", vars));
        Assert.Equal("{\"key\":\"Subj_submitted\",\"args\":[\"Dr. A\",\"2026-10-08\"]}", Subject("submitted", vars));
        // A pharmacy visit (no doctor): the doctor part is empty — the preview leaves it out like the real mail (W5-FIX2 F-4).
        Assert.Equal("{\"key\":\"Subj_adverseevent\",\"args\":[\"\"]}", Subject("adverseevent", "{ institutionName: 'Eczane', visitDate: '2026-10-08' }"));
    }

    [Fact]
    public void N11_a_rep_gets_no_settings_script_no_settings_call_and_the_rep_filter_only_once_a_team_shows_and_arabic_reads_right_to_left()
    {
        var rep = Smoke("rep");
        if (rep is null) return;
        var s = rep.Value;
        Assert.Equal("undefined", s.GetProperty("settingsScript").GetString());
        Assert.Empty(s.GetProperty("settingsCalls").EnumerateArray());
        Assert.True(s.GetProperty("repChipHiddenBefore").GetBoolean());
        Assert.True(s.GetProperty("repChipHiddenOneRep").GetBoolean());
        Assert.False(s.GetProperty("repChipHiddenTwoReps").GetBoolean());
        Assert.Equal(JsonValueKind.Null, s.GetProperty("addressInPreview").ValueKind);

        var ar = Smoke("ar")!.Value;
        var arWords = Resx("ar");
        Assert.Equal(arWords["Event_adverseevent"], ar.GetProperty("cells")[1].GetProperty("eventText").GetString());
        Assert.Contains("<bdi>Murat Demir</bdi>", ar.GetProperty("cells")[1].GetProperty("rep").GetString());
        Assert.Contains(Encode(arWords["Band_adverseevent"]), ar.GetProperty("adverse").GetProperty("body").GetString());
        Assert.Matches("[؀-ۿ]", ar.GetProperty("lookups").GetProperty("filterEvent").GetProperty("items")[0].GetString()!);
    }

    // ═══ 4 · words, menu, routes ══════════════════════════════════════════════════════════════════════════════

    /// <summary>The one value that is the same word in English and another language on purpose: the role acronym in Turkish.</summary>
    private static readonly HashSet<(string Key, string Lang)> SameAsEnglish = [("Rcp_msl", "tr")];

    [Fact]
    public void N12_every_word_of_the_page_is_in_seven_languages_and_every_word_the_scripts_name_is_a_declared_key()
    {
        var keys = VisitNotificationsIndex.ScriptKeys.Concat(VisitNotificationsIndex.ViewKeys).Distinct().ToList();
        var english = Resx("en");
        Assert.Equal(english.OrderBy(x => x.Key), Resx("").OrderBy(x => x.Key));                             // the neutral file is English
        foreach (var language in Languages)
        {
            var words = Resx(language);
            Assert.Equal(keys.Order(), words.Keys.Order());                                                    // exactly the declared keys
            foreach (var key in keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(words[key]), language + " " + key);
                Assert.NotEqual(key, words[key]);
                if (language != "en" && english[key] == words[key])
                {
                    Assert.True(SameAsEnglish.Contains((key, language)), $"{language} {key} is the English word");
                }
            }
        }

        // the placeholders: a translation keeps every {n} of English
        foreach (var language in Languages)
        {
            var words = Resx(language);
            foreach (var key in keys)
            {
                Assert.Equal(Regex.Matches(english[key], @"\{\d\}").Select(m => m.Value).Order(), Regex.Matches(words[key], @"\{\d\}").Select(m => m.Value).Order());
            }
        }

        // every word a script names (L().X, L()['X'], T('X'), key: 'X' through the core) is declared; every view word too
        var scripts = Script("index.js") + Script("settings.js") + Script("notifications-core.js");
        var named = Regex.Matches(scripts, @"L\(\)\.([A-Z][A-Za-z0-9_]+)|L\(\)\['([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'\]|\bT\('([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'|\bkey: '([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'|'((?:Err|St|Rcp|Row|Sec|Safety|Kind|Subj|Band|Title|Intro|Note|Cta|Event|Explain)_[A-Za-z0-9_]*[A-Za-z0-9])'")
            .Select(m => m.Groups.Values.Skip(1).First(g => g.Success).Value).Distinct().ToList();
        var shared = new[] { "Actions", "DateFormat", "Apply", "Reset", "ShowAll", "Cancel" };
        Assert.All(named.Where(n => !shared.Contains(n)), n => Assert.Contains(n, VisitNotificationsIndex.ScriptKeys));
        var views = string.Concat(new[] { "Index.cshtml", "_Filter.cshtml", "_DataTable.cshtml", "_SettingsCard.cshtml" }.Select(Partial));
        Assert.All(Regex.Matches(views, @"(?<!Shared)Localizer\[""([A-Za-z0-9_]+)""\]").Select(m => m.Groups[1].Value), k => Assert.Contains(k, VisitNotificationsIndex.ViewKeys));

        // the bridge merges the payload into window.L10n
        Assert.Contains("document.getElementById('visitnotifications-l10n')", Script("index.l10n.js"));
        Assert.Contains("<script id=\"visitnotifications-l10n\" type=\"application/json\">", Partial("_IndexL10n.cshtml"));
    }

    [Fact]
    public void N13_the_menu_page_its_label_in_seven_languages_and_the_gateway_routes_the_proxy_calls()
    {
        const string Key = "Nav.Page.CRMVISITNOTIFICATIONS";
        var labels = Languages.ToDictionary(l => l, l =>
        {
            var entry = Assert.Single(XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{l}.resx")).Root!.Elements("data").Where(d => (string)d.Attribute("name")! == Key));
            return entry.Element("value")!.Value;
        });
        Assert.All(labels.Values, v => Assert.False(string.IsNullOrWhiteSpace(v) || v.Contains("CRMVISITNOTIFICATIONS", StringComparison.Ordinal)));
        Assert.Equal(7, labels.Values.Distinct().Count());
        Assert.Equal("Gönderilen Ziyaret E-postaları", labels["tr"]);

        var repo = Path.GetDirectoryName(Path.GetDirectoryName(WebRoot()))!;
        var manifest = File.ReadAllText(Path.Combine(repo, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Crm", "SelfRegistration", "CrmManifestProvider.cs"));
        Assert.Contains("new ModuleManifestPage(\"CRM_VISIT_NOTIFICATIONS\", \"Sent Visit E-mails\", \"/CRM/VisitNotifications\", \"crm.visit-notifications.read\"", manifest);
        Assert.Contains("new ModuleManifestAction(\"SETTINGS_MANAGE\", \"Manage Visit Notification Settings\", \"crm.visit-notifications.settings.manage\"", manifest);
        Assert.Equal(VisitNotificationsController.Read, "crm.visit-notifications.read");
        Assert.Equal(VisitNotificationsController.SettingsManage, "crm.visit-notifications.settings.manage");

        // the routes the proxy calls exist on the Gateway with their methods (W3L lesson: a new endpoint's method is pinned)
        var ocelot = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "gateway", "Diten.ApiGateway", "ocelot.json"))).RootElement.GetProperty("Routes").EnumerateArray()
            .Select(r => (Path: r.GetProperty("UpstreamPathTemplate").GetString()!, Methods: r.TryGetProperty("UpstreamHttpMethod", out var m) ? m.EnumerateArray().Select(x => x.GetString()!.ToUpperInvariant()).ToList() : []))
            .ToList();
        bool Routed(string path, string method) => ocelot.Any(r => r.Methods.Contains(method) && Regex.IsMatch(path, "^" + Regex.Replace(Regex.Escape(r.Path), @"\\\{[^}]+\}", "[^?]+") + "$", RegexOptions.IgnoreCase));
        Assert.True(Routed("/api/crm/visit-notifications", "GET"));
        Assert.True(Routed("/api/crm/visit-notifications/" + Row1, "GET"));
        Assert.True(Routed("/api/crm/visit-notifications/settings", "GET"));
        Assert.True(Routed("/api/crm/visit-notifications/settings", "PUT"));

        // the Web routes of the page
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitNotificationsController.cs"));
        foreach (var route in new[] { "[Route(\"CRM/VisitNotifications\")]", "[HttpGet(\"api/list\")]", "[HttpGet(\"api/list/{id:guid}\")]", "[HttpGet(\"api/settings\")]", "[HttpPut(\"api/settings\")]", "[HttpGet(\"api/list/export\")]" })
        {
            Assert.Contains(route, controller);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static (bool, bool) Flags(VisitNotificationsViewModel m) => (m.CanRead, m.CanManageSettings);

    private static VisitNotificationsViewModel Model(IActionResult result)
        => Assert.IsType<VisitNotificationsViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static JsonElement Json(object? value) => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;

    private static string JsArg(string? v) => v is null ? "null" : JsonSerializer.Serialize(v);

    private static string Encode(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    private static string Fmt(string template, params object[] args)
    {
        var s = template;
        for (var i = 0; i < args.Length; i++) s = s.Replace("{" + i + "}", Convert.ToString(args[i], System.Globalization.CultureInfo.InvariantCulture));
        return s;
    }

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        FileContentResult => 200,
        _ => null
    };

    private static (FakeGateway Gateway, VisitNotificationsController Controller) Arrange(string[] permissions, Func<string, string>? answer = null, string? acceptLanguage = null)
    {
        var gateway = new FakeGateway(answer ?? (_ => "{\"data\":{}}"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitNotificationsController(new HttpClient(gateway), configuration, NullLogger<VisitNotificationsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>(new NoTempData());
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        services.AddSingleton<TimeProvider>(new PinnedClock(Today));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    /// <summary>View() reads TempData; the page never writes any.</summary>
    private sealed class NoTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory
    {
        public Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary GetTempData(HttpContext context)
            => new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(context, new NoProvider());

        private sealed class NoProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }
    }

    private sealed class PinnedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The Gateway: answers by path; an answer "!NNN" is that status with an error envelope.</summary>
    private sealed class FakeGateway(Func<string, string> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<(string Method, string Path, string Body)> Calls { get; } = [];

        public List<string> Paths() => Requests.Select(r => r[GatewayUrl.Length..]).ToList();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            lock (Requests)
            {
                Requests.Add(uri);
            }

            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            lock (Calls)
            {
                Calls.Add((request.Method.Method, uri[GatewayUrl.Length..], body));
            }

            var text = answer(uri[GatewayUrl.Length..]);
            if (text.StartsWith('!'))
            {
                var status = int.Parse(text[1..], System.Globalization.CultureInfo.InvariantCulture);
                return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("{\"isSuccessful\":false,\"errors\":[\"refused\"]}", Encoding.UTF8, "application/json") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static JsonElement? Smoke(string mode)
    {
        var words = Resx(mode == "ar" ? "ar" : "tr");
        var texts = VisitNotificationsIndex.ScriptKeys.ToDictionary(k => k, k => words[k]);
        foreach (var (k, v) in new Dictionary<string, string> { ["ShowAll"] = "Tümü", ["Apply"] = "Uygula", ["Reset"] = "Sıfırla", ["Actions"] = words["ColActions"] }) texts.TryAdd(k, v);
        texts["DateFormat"] = Diten.Web.Infrastructure.DateFieldFormat.Flatpickr(System.Globalization.CultureInfo.GetCultureInfo(mode == "ar" ? "ar" : "tr"));
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-notifications-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var file = Path.Combine(Path.GetTempPath(), "vn-words-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(texts), new UTF8Encoding(false));
        try
        {
            var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + file.Replace("\\", "/") + "'; process.argv[4] = '" + mode + "'; require('" + smoke + "');");
            return node is null ? null : JsonDocument.Parse(node).RootElement;
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string Core() => "const C = require('" + Path.Combine(ScriptDir(), "notifications-core.js").Replace("\\", "/") + "');\n";

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the C# assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vn-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitNotifications");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string Partial(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitNotifications", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitNotificationsIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitNotifications", file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
