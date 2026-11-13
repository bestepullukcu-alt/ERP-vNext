using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.VisitQrCodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-FIX on the REAL views, scripts, resx and controller: U-1 Q5 on the Golden Compact server-mode list
/// standard (the shell's v2 marker, the factory call, the proxy's translation of the list contract, the export, the bulk
/// bar and its two actions, the row renderers in a fake factory), U-2 the W3E form fields' icons (ICON_MAP), U-3 the list
/// header, G-2 the type label, G-3 the city in words, G-4 a name never an id, G-6 the names isolated for RTL.
/// </summary>
public sealed partial class VisitQrCodesWebTests
{
    // ═══ U-1 · the list standard ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void U1_the_q5_table_is_the_shared_v2_shell_in_server_mode_with_the_standard_partials()
    {
        var dataTable = View("VisitQrCodes", "_DataTable.cshtml");
        Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=\"listShell\" />", dataTable);
        Assert.Contains("TableId = \"dt-visitqrcodes\"", dataTable);
        Assert.Contains("DataMode = \"server\"", dataTable);
        Assert.Contains("HasSelection = true", dataTable);

        // the table Q5 renders IS the shell's: the v2 standard marker + the data mode on the <table>, the shaped placeholder
        var shell = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "Components", "DataTable", "_ListShell.cshtml"));
        var table = Regex.Match(shell, "<table [^>]*>").Value;
        Assert.Contains("data-dt-standard=\"v2\"", table);
        Assert.Contains("data-dt-data-mode=\"@Model.DataMode\"", table);
        Assert.Contains("<partial name=\"_TableSkeleton\" />", shell);

        var index = View("VisitQrCodes", "Index.cshtml");
        foreach (var marker in new[] { "<partial name=\"~/Views/CRM/VisitQrCodes/_Filter.cshtml\" />", "<partial name=\"~/Views/CRM/VisitQrCodes/_DataTable.cshtml\" />",
                     "<partial name=\"~/Views/CRM/VisitQrCodes/_IndexL10n.cshtml\" />", "<partial name=\"~/Views/Shared/Components/DataTable/_BulkActionBar.cshtml\" model=\"bulkActionBar\" />",
                     "id=\"offcanvasDetailsPreview\"", "Layout = \"_LayoutTenantShell\";", "~/assets/js/CRM/VisitQrCodes/index.l10n.js", "~/assets/js/CRM/VisitQrCodes/index.js" })
        {
            Assert.Contains(marker, index);
        }

        Assert.DoesNotContain("qr-codes.js", index);
        Assert.False(File.Exists(Path.Combine(ScriptDir(), "qr-codes.js")));                               // the custom table is gone

        var filter = View("VisitQrCodes", "_Filter.cshtml");
        foreach (var chip in new[] { "filterRegion", "filterType", "filterCity", "filterStatus" })
        {
            Assert.Matches("<select id=\"" + chip + "\" class=\"form-select form-select-sm select2\" data-placeholder=", filter);
        }

        Assert.Contains("id=\"inlineFilterCollapse\"", filter);
        Assert.Contains("class=\"pt-0 pb-3\"", filter);
        Assert.True(filter.IndexOf("@if (Model.CanReadAccounts)", StringComparison.Ordinal) < filter.IndexOf("id=\"filterRegion\"", StringComparison.Ordinal));

        // the factory call: server mode, the export, NO page-written serverSide / ajax.data / ext.search, no hard page cap
        var js = Script("index.js");
        Assert.Contains("window.DitenDataTable.createList({", js);
        Assert.Contains("dataMode: 'server'", js);
        Assert.Contains("const endpoint = '/CRM/VisitQrCodes/api';", js);
        Assert.Contains("export: { mode: 'server', url: endpoint + '/accounts/export'", js);
        var code = Regex.Replace(Regex.Replace(js, @"/\*[\s\S]*?\*/", ""), @"(^|[^:])//.*$", "$1", RegexOptions.Multiline);
        Assert.DoesNotContain("serverSide", code);
        Assert.DoesNotContain("ext.search", code);
        Assert.DoesNotMatch(@"\bdata\s*:\s*function", code);
        Assert.DoesNotMatch(@"pageSize=\d", code);
    }

    [Fact]
    public void U3_the_q5_header_is_the_list_header_standard_and_the_crumb_is_gone()
    {
        var index = View("VisitQrCodes", "Index.cshtml");
        Assert.Matches(@"<div class=""mb-3"">\s*<h5 class=""mb-0"">@Localizer\[""VisitQrCodesTitle""\]</h5>\s*<p class=""mb-0 text-muted"">@Localizer\[""PageDescription""\]</p>\s*</div>", index);
        Assert.DoesNotContain("vq-crumb", index);
        Assert.DoesNotContain("<h4", index);
    }

    [Fact]
    public async Task U1_the_proxy_turns_the_list_contract_into_the_crm_query_and_answers_items_total_filtered_total()
    {
        // unfiltered: ONE call, total = filteredTotal
        var (gw, c) = ArrangeRouted([VisitQrCodesController.Read], _ => "{\"data\":{\"items\":[{\"accountId\":\"a\"}],\"total\":43374,\"page\":3,\"pageSize\":25}}");
        var plain = Assert.IsType<OkObjectResult>(await c.List(50, 25, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Equal(new[] { "/api/crm/visit-qr/accounts?page=3&pageSize=25" }, gw.Paths());
        var body = JsonDocument.Parse(JsonSerializer.Serialize(plain.Value)).RootElement.GetProperty("data");
        Assert.Equal((43374, 43374, 1), (body.GetProperty("total").GetInt64(), body.GetProperty("filteredTotal").GetInt64(), body.GetProperty("items").GetArrayLength()));

        // filtered: the matched page + the tenant's whole count (one row)
        var (gw2, c2) = ArrangeRouted([VisitQrCodesController.Read], path => path.Contains("pageSize=1", StringComparison.Ordinal) && !path.Contains("status=")
            ? "{\"data\":{\"items\":[{}],\"total\":43374}}" : "{\"data\":{\"items\":[],\"total\":120}}");
        var filtered = Assert.IsType<OkObjectResult>(await c2.List(0, 10, "acı", null, null, "none", "pharmacy", null, "n1", CancellationToken.None));
        Assert.Equal(new[] { "/api/crm/visit-qr/accounts?status=none&accountType=pharmacy&territoryId=n1&search=ac%C4%B1&page=1&pageSize=10", "/api/crm/visit-qr/accounts?page=1&pageSize=1" }, gw2.Paths());
        var fb = JsonDocument.Parse(JsonSerializer.Serialize(filtered.Value)).RootElement.GetProperty("data");
        Assert.Equal((43374, 120), (fb.GetProperty("total").GetInt64(), fb.GetProperty("filteredTotal").GetInt64()));

        // the contract's refusals never reach the CRM: a sort key (the CRM orders by name), a page size outside 1–100
        var (gw3, c3) = ArrangeRouted([VisitQrCodesController.Read], _ => "{}");
        Assert.Equal(400, Status(await c3.List(0, 10, null, "accountName", "asc", null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.List(0, 101, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.List(0, 0, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.List(0, 10, null, null, "sideways", null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw3.Requests);

        // no read key: nothing reaches the CRM
        var (gw4, c4) = ArrangeRouted([], _ => "{}");
        Assert.Equal(403, Status(await c4.List(0, 10, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw4.Requests);
    }

    [Fact]
    public async Task U1_the_export_writes_every_matching_institution_page_by_page_in_the_readers_language_and_refuses_over_50000()
    {
        static string Page(int from, int count, long total) => "{\"data\":{\"total\":" + total + ",\"items\":[" + string.Join(",", Enumerable.Range(from, count).Select(i =>
            "{\"accountId\":\"" + Guid.NewGuid() + "\",\"accountName\":\"Kurum " + i + "\",\"accountType\":\"pharmacy\",\"cityRef\":\"TR-42-KONYA\",\"status\":\"" + (i % 2 == 0 ? "active" : "none")
            + "\",\"version\":" + (i % 2 == 0 ? "2" : "null") + ",\"recentScanCount\":" + i + (i == 0 ? ",\"parentAccountName\":\"75.YIL MECİDİYEKÖY ASM\"" : "") + "}")) + "]}}";
        var (gw, c) = ArrangeRouted([VisitQrCodesController.Read], path => path.Contains("page=1&", StringComparison.Ordinal) ? Page(0, 100, 101) : Page(100, 1, 101), "tr");
        var file = Assert.IsType<FileContentResult>(await c.Export("csv", ["accountName,accountType,cityRef,status,version"], "kurum", null, null, "active", null, null, null, CancellationToken.None));
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.StartsWith("institution-qr-codes-", file.FileDownloadName);
        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart('﻿').Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(102, csv.Length);                                                                         // the header + EVERY match (2 pages)
        Assert.Equal("Kurum,Tür,Şehir,QR durumu,Sürüm", csv[0]);                                             // the screen's words, in Turkish
        Assert.Equal("Kurum 0 (↳ 75.YIL MECİDİYEKÖY ASM),pharmacy,Konya,Aktif,2", csv[1]);                    // G-3: TR-42-KONYA → Konya
        Assert.Contains(gw.Paths(), p => p == "/api/crm/visit-qr/accounts?status=active&search=kurum&page=2&pageSize=100");
        Assert.All(gw.Paths(), p => Assert.DoesNotContain("start=", p));

        // more than 50 000 → 413 EXPORT_TOO_LARGE after ONE read, nothing written
        var (gw2, c2) = ArrangeRouted([VisitQrCodesController.Read], _ => Page(0, 1, 50_001));
        var big = Assert.IsType<ObjectResult>(await c2.Export("xlsx", null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Equal(413, big.StatusCode);
        Assert.Contains("EXPORT_TOO_LARGE", JsonSerializer.Serialize(big.Value));
        Assert.Single(gw2.Requests);

        // an unknown column / format → 400, no read
        var (gw3, c3) = ArrangeRouted([VisitQrCodesController.Read], _ => Page(0, 1, 1));
        Assert.Equal(400, Status(await c3.Export("csv", ["accountName,payload"], null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.Export("pdf", null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Empty(gw3.Requests);
        Assert.Equal(403, Status(await ArrangeRouted([], _ => "{}").Controller.Export("csv", null, null, null, null, null, null, null, null, CancellationToken.None)));
    }

    [Fact]
    public void U1_the_bulk_bar_carries_generate_and_print_and_the_generate_route_is_the_lists_bulk_endpoint()
    {
        var route = typeof(VisitQrCodesController).GetMethod(nameof(VisitQrCodesController.BulkIssue))!.GetCustomAttribute<HttpPostAttribute>()!.Template;
        Assert.Equal("api/accounts/bulk", route);
        var index = View("VisitQrCodes", "Index.cshtml");
        Assert.Contains("Key = \"issue\"", index);
        Assert.Contains("ConfirmKey = \"BulkGenerateConfirm\"", index);
        Assert.Contains("Endpoint = \"/CRM/VisitQrCodes/api/accounts/bulk\"", index);
        Assert.Contains("Key = \"print\"", index);
        var js = Script("index.js");
        Assert.Contains("window.showConfirm?.(fmt(L().BulkGenerateConfirm, ids.length)", js);               // the confirm standard
        Assert.Contains("list.reload('BulkIssueResult')", js);                                                  // the standard result toast
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void U1_the_rows_in_a_fake_factory_institution_parent_type_city_chip_actions_quick_view_and_the_two_bulk_actions(bool manage)
    {
        var node = RunNode(ListHarness(manage));
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var o = r.GetProperty("options");
        Assert.Equal(("server", "/CRM/VisitQrCodes/api/accounts", "/CRM/VisitQrCodes/api/accounts/export", ""), (o.GetProperty("dataMode").GetString(), o.GetProperty("ajax").GetString(),
            o.GetProperty("export").GetString(), o.GetProperty("addNew").GetString()));
        Assert.Equal(manage, o.GetProperty("bulk").GetBoolean());                                             // no bulk without manage
        Assert.Equal(manage, o.GetProperty("checkboxVisible").GetBoolean());                                  // …and no selection column
        Assert.Equal("territoryId,accountType,city,status", o.GetProperty("filters").GetString());
        Assert.Equal("CRM/VisitQrCodes", o.GetProperty("savedView").GetString());
        Assert.False(o.GetProperty("anySortable").GetBoolean());                                              // the CRM orders by name

        var a = r.GetProperty("active");
        Assert.Equal("<a class=\"fw-medium text-heading\" href=\"/CRM/Accounts/Details/a1\"><bdi>75.YIL MECİDİYEKÖY ASM</bdi></a><div class=\"small text-muted\">↳ <bdi>İstanbul İl Sağlık</bdi></div>",
            a.GetProperty("name").GetString());                                                                // G-6: isolated names
        Assert.Equal(("Eczane", "<bdi>Konya</bdi>"), (a.GetProperty("type").GetString(), a.GetProperty("city").GetString())); // G-2 · G-3
        Assert.Contains("bg-label-success", a.GetProperty("chip").GetString());
        Assert.Contains("Aktif", a.GetProperty("chip").GetString());
        Assert.Equal(("v3", "12"), (a.GetProperty("version").GetString(), a.GetProperty("recent").GetString()));
        Assert.Equal(manage ? "[quickView][print][open]" : "[quickView][open]", a.GetProperty("actions").GetString()); // print = manage + an active code
        var n = r.GetProperty("none");
        Assert.Equal(("clinic", "Kirikkale"), (n.GetProperty("type").GetString().Replace("<bdi>", "").Replace("</bdi>", ""), n.GetProperty("city").GetString().Replace("<bdi>", "").Replace("</bdi>", "")));
        Assert.Contains(Resx("tr")["InheritsParent"], n.GetProperty("chip").GetString());
        Assert.Equal(("—", "—", "[quickView][open]"), (n.GetProperty("version").GetString(), n.GetProperty("recent").GetString(), n.GetProperty("actions").GetString()));

        var qv = r.GetProperty("quickView");
        Assert.Equal(("75.YIL MECİDİYEKÖY ASM", "auto", "↳ İstanbul İl Sağlık", "badge bg-label-success"), (qv.GetProperty("title").GetString(), qv.GetProperty("titleDir").GetString(),
            qv.GetProperty("subtitle").GetString(), qv.GetProperty("statusClass").GetString()));
        Assert.Equal(!manage, qv.GetProperty("printHidden").GetBoolean());

        if (!manage) { return; }
        var bulk = r.GetProperty("bulk");
        Assert.Equal(Fmt(QrCodesResx("tr")["BulkGenerateConfirm"], 2), bulk.GetProperty("confirm").GetString()); // the showConfirm standard
        Assert.Equal("POST /CRM/VisitQrCodes/api/accounts/bulk {\"accountIds\":[\"a1\",\"a2\"]}", bulk.GetProperty("post").GetString());
        Assert.Equal("BulkIssueResult", bulk.GetProperty("reloadKey").GetString());                          // the standard reload-with-toast
        Assert.Equal(Fmt(Resx("tr")["BulkResult"], 1, 1), bulk.GetProperty("reloadText").GetString());        // "1 kod üretildi · 1 … atlandı"
        Assert.Equal("/CRM/VisitQrCodes/Print?mode=labels&ids=a1%2Ca2", bulk.GetProperty("print").GetString());
        Assert.Equal((Resx("tr")["BulkLimit"], 0), (bulk.GetProperty("limitToast").GetString(), bulk.GetProperty("postsAfterLimit").GetInt32())); // > 200: refused, nothing sent
    }

    // ═══ U-2 · the field icons ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>The W3E block of ICON_MAP (frontend/Diten.Web/tests/diten-field-icons.test.js) — vitest does not start on every
    /// machine, so the rule "every W3E field carries its icon" is held here too, from the SAME map.</summary>
    private static Dictionary<string, string> W3EIconMap()
    {
        var test = File.ReadAllText(Path.Combine(WebRoot(), "tests", "diten-field-icons.test.js"));
        var map = test[test.IndexOf("const ICON_MAP = {", StringComparison.Ordinal)..];
        map = map[..map.IndexOf("\n};", StringComparison.Ordinal)];
        return Regex.Matches(map, "\"((?:vq|vf)-[a-z-]+)\":\\s*\"(bx-[a-z0-9-]+)\"").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    [Fact]
    public void U2_every_w3e_field_is_a_diten_field_with_its_icon_map_glyph_before_the_control()
    {
        var map = W3EIconMap();
        Assert.Equal(new[] { "vf-from", "vf-to", "vq-copies", "vq-paper-labels-select", "vq-paper-single-select", "vq-regen-reason", "vq-revoke-reason" }, map.Keys.OrderBy(k => k, StringComparer.Ordinal));
        var views = new Dictionary<string, string>
        {
            ["vq-regen-reason"] = View("Accounts", "_VisitQrCard.cshtml"), ["vq-revoke-reason"] = View("Accounts", "_VisitQrCard.cshtml"),
            ["vq-paper-single-select"] = View("VisitQrCodes", "Print.cshtml"), ["vq-paper-labels-select"] = View("VisitQrCodes", "Print.cshtml"),
            ["vq-copies"] = View("VisitQrCodes", "Print.cshtml"),
            ["vf-from"] = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceFlags", "_Filter.cshtml")),
            ["vf-to"] = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceFlags", "_Filter.cshtml"))
        };
        foreach (var (id, glyph) in map)
        {
            var text = views[id];
            var at = text.IndexOf("id=\"" + id + "\"", StringComparison.Ordinal);
            Assert.True(at > 0, id + " is gone");
            var opened = text.LastIndexOf("class=\"diten-field", at, StringComparison.Ordinal);
            Assert.True(opened > 0, id + " has no .diten-field");
            var wrapper = text[opened..at];
            Assert.False(wrapper.Contains("</div>", StringComparison.Ordinal) || wrapper.Contains("</span>", StringComparison.Ordinal), id + ": its wrapper closed before the control");
            Assert.Matches("<i class=\"bx " + glyph + " diten-field-icon[^\"]*\" aria-hidden=\"true\"></i>", wrapper);  // the glyph BEFORE the control, hidden
            Assert.Matches("class=\"form-(control|select)", text[at..Math.Min(text.Length, at + 200)] + text[(at - 120)..at]);
        }

        // the textareas park the glyph on the first line; the labels sit OUTSIDE (above) the wrapper
        var card = View("Accounts", "_VisitQrCard.cshtml");
        Assert.Equal(2, Regex.Matches(card, "diten-field-icon diten-field-icon--top").Count);
        Assert.Contains("<label class=\"vq-label-cap\" for=\"vq-regen-reason\">", card);
        Assert.Contains("<label class=\"vq-label-cap\" for=\"vq-revoke-reason\">", card);
        Assert.DoesNotContain("data-paper=", View("VisitQrCodes", "Print.cshtml"));                          // the paper is a field now
        Assert.Contains("['vq-paper-single-select', 'vq-paper-labels-select'].forEach", Script("qr-print.js"));
        // every glyph exists in the icon set (a missing one renders a blank gap)
        var icons = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "vendor", "fonts", "iconify-icons.css"));
        Assert.All(map.Values.Distinct(), g => Assert.Contains("." + g, icons));
    }

    // ═══ G-2 · G-3 · G-4 · G-6 ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void G3_the_city_in_words_the_set_label_by_the_code_or_its_name_else_a_readable_name_never_the_raw_code()
    {
        var cities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["konya"] = "Konya", ["istanbul"] = "İstanbul", ["adiyaman"] = "Adıyaman", ["agri"] = "Ağrı" };
        Assert.Equal("Konya", CrmCityLabels.Resolve("TR-42-KONYA", cities));
        Assert.Equal("İstanbul", CrmCityLabels.Resolve("TR-34-ISTANBUL", cities));
        Assert.Equal("Adıyaman", CrmCityLabels.Resolve("TR-02-ADIYAMAN", cities));
        Assert.Equal("Ağrı", CrmCityLabels.Resolve("TR-04-AĞRI", cities));                                    // a Turkish code still finds the set
        Assert.Equal("Konya", CrmCityLabels.Resolve("konya", cities));
        Assert.Equal("Kirikkale", CrmCityLabels.Resolve("TR-71-KIRIKKALE", cities));                         // not in the set: readable
        Assert.Equal("İstanbul", CrmCityLabels.Resolve("TR-34-İSTANBUL", null));                             // Turkish İ cased the Turkish way
        Assert.Equal("Yeni Mahalle", CrmCityLabels.Resolve("TR-06-YENI-MAHALLE", null));
        Assert.Equal("Xyz", CrmCityLabels.Resolve("Xyz", null));
        Assert.Null(CrmCityLabels.Resolve("  ", cities));
        Assert.Contains("CrmCityLabels.Resolve(cityCode, await ReadReferenceLabelsAsync(CitySet, ct))",
            File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitQrCodesController.cs")));   // the print address uses it

        var node = RunNode(Core() + """
            const cities = { konya: 'Konya', istanbul: 'İstanbul' };
            process.stdout.write(JSON.stringify([C.cityLabel('TR-42-KONYA', cities), C.cityLabel('TR-34-ISTANBUL', cities), C.cityLabel('TR-71-KIRIKKALE', cities),
              C.cityLabel('TR-34-İSTANBUL', {}), C.cityLabel('konya', cities), C.cityLabel('', cities),
              C.listRow({ accountType: 'PHARMACY', cityRef: 'TR-42-KONYA' }, { types: { pharmacy: 'Eczane' }, cities }).typeLabel,
              C.listRow({ accountType: 'lab', cityRef: null }, { types: { pharmacy: 'Eczane' } }).typeLabel,
              C.listRow({ accountType: 'lab', cityRef: null }, {}).cityLabel]));
            """);
        if (node is null) { return; }
        Assert.Equal("[\"Konya\",\"İstanbul\",\"Kirikkale\",\"İstanbul\",\"Konya\",null,\"Eczane\",\"lab\",\"—\"]", node);  // G-2: the label, else the code
    }

    [Fact]
    public void G4_the_card_and_the_history_show_the_name_and_never_the_id()
    {
        const string Id = "0a0a0a0a-0000-0000-0000-00000000000a";
        var named = RunNode(CardHarness(true, "{ status: 'active', version: 3, issuedBy: '" + Id + "', issuedByName: 'Ayşe Yılmaz', issuedAt: '2026-10-12T12:18:00Z', recentScanCount: 1 }"));
        if (named is null) { return; }
        var html = JsonDocument.Parse(named).RootElement.GetProperty("state").GetString()!;
        Assert.Contains("Üreten <bdi>Ayşe Yılmaz</bdi>", html);
        Assert.DoesNotContain(Id, html);

        // no name resolved (another tenant's user, an unknown id, the directory down): "—", NEVER the id
        var bare = JsonDocument.Parse(RunNode(CardHarness(true, "{ status: 'active', version: 3, issuedBy: '" + Id + "', issuedByName: null, issuedAt: '2026-10-12T12:18:00Z', recentScanCount: 1 }"))!).RootElement.GetProperty("state").GetString()!;
        Assert.Contains("Üreten <bdi>—</bdi>", bare);
        Assert.DoesNotContain(Id, bare);
        var revoked = JsonDocument.Parse(RunNode(CardHarness(true, "{ status: 'revoked', version: 3, revokedBy: '" + Id + "', revokedByName: '" + Id + "', revokedAt: '2026-10-13T06:40:00Z' }"))!).RootElement.GetProperty("state").GetString()!;
        Assert.DoesNotContain(Id, revoked);                                                                    // an id in the name field is not a name either

        var node = RunNode(Core() + """
            process.stdout.write(JSON.stringify([C.eventLine({ kind: 'issued', version: 1, by: '__ID__', byName: 'Ayşe Yılmaz' }).by,
              C.eventLine({ kind: 'issued', version: 1, by: '__ID__' }).by, C.personName('__ID__'), C.personName(' Bora ')]));
            """.Replace("__ID__", Id));
        Assert.Equal("[\"Ayşe Yılmaz\",null,null,\"Bora\"]", node);
        Assert.Contains("bidi(line.by || '—')", Script("qr-card.js"));                                       // the history: the name or "—"
    }

    [Fact]
    public void G6_every_institution_and_person_name_is_isolated_for_right_to_left()
    {
        var print = View("VisitQrCodes", "Print.cshtml");
        Assert.Contains("<div class=\"vq-s-parent\" id=\"vq-s-parent\"><bdi>@(Model.ParentName ?? string.Empty)</bdi></div>", print);
        Assert.Contains("<div class=\"vq-s-name\" id=\"vq-s-name\"><bdi>@(Model.AccountName ?? \"—\")</bdi></div>", print);
        Assert.Contains("<div class=\"vq-s-addr\"><bdi>@(Model.Address ?? string.Empty)</bdi></div>", print);
        var printJs = Script("qr-print.js");
        Assert.Contains("'<div class=\"vq-l-parent\"><bdi>' + esc(item.parent || '') + '</bdi></div>'", printJs);
        Assert.Contains("'<div class=\"vq-l-name\"><bdi>' + esc(item.name || '—') + '</bdi></div>'", printJs);
        var card = Script("qr-card.js");
        Assert.DoesNotMatch(@"esc\((m\.issuedBy|m\.revokedBy|line\.by|accountName|m\.inherited\.accountName)", card);   // names only through bidi()
        Assert.Contains("<div class=\"vq-dlg-sub\"><bdi>@account.AccountName</bdi></div>", View("Accounts", "_VisitQrCard.cshtml"));
    }

    // ── helpers (W3E-FIX) ──────────────────────────────────────────────────────────────────────────────────────

    private static string Fmt(string template, params object[] args)
    {
        var s = template;
        for (var i = 0; i < args.Length; i++) { s = s.Replace("{" + i + "}", Convert.ToString(args[i], System.Globalization.CultureInfo.InvariantCulture)); }
        return s;
    }

    private static Dictionary<string, string> QrCodesResx(string culture)
    {
        var file = "VisitQrCodesIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitQrCodes", file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    /// <summary>index.js in Node over a FAKE list factory: the options it hands the factory, its renderers on two rows, the
    /// quick view, and (manage) the two bulk actions through the standard confirm / reload.</summary>
    private static string ListHarness(bool manage)
    {
        var qr = Resx("tr");
        var page = QrCodesResx("tr");
        var l10n = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["ShowAll"] = "Tümü", ["Actions"] = page["Actions"], ["QuickView"] = page["QuickView"], ["BulkGenerate"] = qr["BulkGenerate"],
            ["BulkGenerateConfirm"] = page["BulkGenerateConfirm"], ["BulkResult"] = qr["BulkResult"], ["BulkLimit"] = qr["BulkLimit"], ["RowPrint"] = qr["RowPrint"],
            ["RowOpen"] = qr["RowOpen"], ["ChipActive"] = qr["Chip_active"], ["ChipRevoked"] = qr["Chip_revoked"], ["ChipNone"] = qr["Chip_none"],
            ["InheritsParent"] = qr["InheritsParent"], ["InheritedInfo"] = qr["InheritedInfo"], ["VersionShort"] = qr["VersionShort"], ["Today"] = qr["Today"],
            ["Yesterday"] = qr["Yesterday"], ["ErrGeneric"] = qr["Err_generic"], ["ErrorOccurred"] = "Hata"
        });
        return "const C = require('" + Path.Combine(ScriptDir(), "qr-core.js").Replace("\\", "/") + "');\n" + """
            const els = {};
            const mk = id => ({ id, attrs: {}, textContent: '', innerHTML: '', className: '', _cls: new Set(),
              classList: { toggle(c, on) { const s = this._o._cls; if (on === undefined ? !s.has(c) : on) s.add(c); else s.delete(c); }, contains(c) { return this._o._cls.has(c); } },
              getAttribute(n) { return this.attrs[n] == null ? null : this.attrs[n]; }, setAttribute(n, v) { this.attrs[n] = String(v); } });
            const get = id => { if (!els[id]) { els[id] = mk(id); els[id].classList._o = els[id]; } return els[id]; };
            Object.assign(get('vq-list-root').attrs, { 'data-can-manage': '__MANAGE__', 'data-can-read-accounts': 'true' });
            ['filterRegion', 'filterType', 'filterCity', 'filterStatus'].forEach(get);
            let ready = null, options = null;
            const reloads = [], confirms = [], posts = [], toasts = [], assigned = [];
            const rows = [
              { accountId: 'a1', accountName: '75.YIL MECİDİYEKÖY ASM', parentAccountName: 'İstanbul İl Sağlık', accountType: 'pharmacy', cityRef: 'TR-42-KONYA', status: 'active', version: 3, lastScannedAt: null, recentScanCount: 12 },
              { accountId: 'a2', accountName: 'Göğüs Polikliniği', accountType: 'clinic', cityRef: 'TR-71-KIRIKKALE', status: 'none', version: null, recentScanCount: 0, inheritedFrom: { accountId: 'p', accountName: 'Acıbadem', version: 2 } }
            ];
            global.document = { documentElement: { lang: 'tr' }, getElementById: get, querySelector: q => (q === '.datatables-visitqrcodes' ? get('dt') : null), addEventListener: (e, f) => { ready = f; } };
            global.fetch = (url, init) => {
              if (init && init.method === 'POST') { posts.push('POST ' + url + ' ' + init.body); return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ data: { issued: 1, skippedActive: 1 } }) }); }
              const body = url === '/CRM/VisitQrCodes/api/lookups' ? { data: { accountTypes: [{ value: 'pharmacy', text: 'Eczane' }], cities: [{ value: 'konya', text: 'Konya' }] } } : { nodes: [{ id: 'n1', name: 'Marmara' }] };
              return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) });
            };
            global.window = {
              VisitQrCore: C, L10n: __L10N__,
              DitenDataTable: {
                getAuthHeaders: () => ({}),
                renderActions: acts => acts.filter(a => a.visible !== false).map(a => '[' + a.key + ']').join(''),
                createList: async o => { options = o; return { reload: k => reloads.push([k, global.window.L10n[k]]), dt: { rows: () => ({ data: () => ({ toArray: () => rows }) }) } }; }
              },
              showConfirm: (text, cb, opts) => { confirms.push(text); cb(); },
              showToast: (m, t) => toasts.push(m),
              location: { assign: u => assigned.push(u) },
              sessionStorage: { setItem() {} }
            };
            require('__INDEX__');
            const tick = () => new Promise(r => setTimeout(r, 15));
            (async () => {
              ready(); await tick(); await tick();
              const defs = options.config.columnDefs;
              const def = t => defs.find(d => d.targets === t);
              const cell = (t, row) => def(t).render(row.accountId, 'display', row);
              const shape = row => ({ name: cell(2, row), type: cell(3, row), city: cell(4, row), chip: cell(5, row), version: cell(6, row), recent: String(cell(8, row)), actions: def(-1).render(null, 'display', row) });
              const out = {
                options: { dataMode: options.dataMode, ajax: options.ajax.url, export: options.export.url, addNew: options.toolbar.addNewText, bulk: !!options.bulk,
                  checkboxVisible: def(1).visible, filters: options.filters.fields.map(f => f.key).join(','), savedView: options.savedView.moduleKey + '/' + options.savedView.pageKey,
                  anySortable: defs.some(d => d.orderable === true) || !defs.some(d => d.targets === '_all' && d.orderable === false),
                  priorities: Object.fromEntries(defs.filter(d => typeof d.targets === 'number').map(d => [d.targets, (d.className || '').indexOf('all') > -1 ? 'all' : (d.responsivePriority == null ? null : d.responsivePriority)])) },
                active: shape(rows[0]), none: shape(rows[1])
              };
              options.quickView.populate(rows[0]);
              out.quickView = { title: get('oc-title').textContent, titleDir: get('oc-title').getAttribute('dir'), subtitle: get('oc-subtitle').textContent, statusClass: get('oc-status').className,
                printHidden: get('oc-btn-print').classList.contains('d-none') };
              if (options.bulk) {
                options.bulk.onBulkAction.issue({ ids: ['a1', 'a2'] }); await tick(); await tick();
                options.bulk.onBulkAction.print({ ids: ['a1', 'a2'] });
                const before = posts.length;
                options.bulk.onBulkAction.issue({ ids: Array.from({ length: 201 }, (_, i) => 'x' + i) }); await tick();
                out.bulk = { confirm: confirms[0], post: posts[0], reloadKey: reloads[0] && reloads[0][0], reloadText: reloads[0] && reloads[0][1], print: assigned[0],
                  limitToast: toasts[toasts.length - 1], postsAfterLimit: posts.length - before };
              }
              process.stdout.write(JSON.stringify(out));
            })().catch(e => { console.error(e); process.exit(1); });
            """.Replace("__MANAGE__", manage ? "true" : "false").Replace("__L10N__", l10n).Replace("__INDEX__", Path.Combine(ScriptDir(), "index.js").Replace("\\", "/"));
    }

    private static (RoutedGateway Gateway, VisitQrCodesController Controller) ArrangeRouted(string[] permissions, Func<string, string> answer, string? acceptLanguage = null)
    {
        var gateway = new RoutedGateway(answer);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitQrCodesController(new HttpClient(gateway), configuration, new ListLogger());
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private sealed class RoutedGateway(Func<string, string> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Paths() => Requests.Select(r => r[GatewayUrl.Length..]).ToList();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            Requests.Add(uri);
            // the reference sets (labels) are not this test's subject: an empty set
            var body = uri.Contains("reference", StringComparison.OrdinalIgnoreCase) ? "{\"data\":{\"items\":[]}}" : answer(uri[GatewayUrl.Length..]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
