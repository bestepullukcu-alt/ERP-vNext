using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.Commercial;
using Diten.Web.Models.Commercial;
using Diten.Web.Views.Commercial;
using Diten.Web.Views.Commercial.PreOrders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W6 · W6-WEB-a — the pharmacy pre-order screens (mockup W6 A / B / C / C-link / E + Details + settings) and the
/// shared commercial rule message source, on the REAL views, proxies, resx and scripts: (1) every page draws only the
/// access notice without its key (UAS-001) and the list is the Golden Compact v2 shell in server mode; (2) every proxy
/// write / read checks its MOD-0168 §14 key HERE before the CRM is asked, and forwards to the right CRM path; (3) the
/// e-mail and Work Center link paths are the CRM's constants; (4) the rule messages cover EVERY CRM reason / refusal code
/// in seven languages with the contract's placeholders (the test reads the CRM constants); (5) quantity mode draws no
/// price / discount field, the depot's answer exists only for sales operations, screen E has the two copies; (6) the page
/// rules in Node (period twin, MF, send blockers, lock, limits, six months per currency, link states, rule wording);
/// (7) every word, menu label and help text in seven languages; the manifest pages; the gateway routes the proxy calls.
/// </summary>
public sealed class PreOrdersWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c5e1b2-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Today = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private const string OrderId = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid Order = Guid.Parse(OrderId);

    private const string Read = PreOrdersController.Read;
    private const string ReadAll = PreOrdersController.ReadAll;
    private const string Create = PreOrdersController.Create;
    private const string Approve = PreOrdersController.Approve;

    // ═══ 1 · the pages ═════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Each page file → the flag its UAS-001 guard reads.</summary>
    public static TheoryData<string, string> Guards() => new()
    {
        { "PreOrders/Index.cshtml", "@if (!Model.CanRead)" },
        { "PreOrders/Create.cshtml", "@if (!Model.CanCreate)" },
        { "PreOrders/Edit.cshtml", "@if (!Model.CanCreate)" },
        { "PreOrders/Details.cshtml", "@if (!Model.CanRead)" },
        { "PreOrders/Approve.cshtml", "@if (!Model.CanApprove || !Model.CanRead)" },
        { "PreOrders/ApprovalLink.cshtml", "@if (!Model.CanApprove)" },
        { "PreOrders/Document.cshtml", "@if (!Model.CanRead)" },
        { "PreOrderSettings/Index.cshtml", "@if (!Model.CanRead)" }
    };

    [Theory]
    [MemberData(nameof(Guards))]
    public void P1_every_page_draws_only_the_access_notice_without_its_key_and_sits_in_the_tenant_shell(string file, string guard)
    {
        var view = View(file);
        Assert.Contains("Layout = \"_LayoutTenantShell\";", view);                          // explicitly (§9), not _ViewStart
        var at = view.IndexOf(guard, StringComparison.Ordinal);
        Assert.True(at > 0, file + " has no guard");
        // the guard is the first thing the page body does: no markup before it, the notice and return inside it
        var before = view[..at];
        Assert.DoesNotMatch(@"<(div|section|partial|form|h5|table)\b", Regex.Replace(before, @"@\*[\s\S]*?\*@", ""));
        Assert.Matches(Regex.Escape(guard) + @"\s*\{\s*<partial name=""_AccessDenied"" model=""@Localizer\[""[A-Za-z]+""\]\.Value"" />\s*return;\s*\}", view);
        var markup = Regex.Replace(view, @"@\*[\s\S]*?\*@", "");
        Assert.DoesNotContain("_CreateEditOffcanvas", markup);                                  // Compact (§11)
        Assert.DoesNotContain("breadcrumb", markup);                                            // Golden header, C has none of its own
        Assert.DoesNotContain("<h4", markup);
        Assert.DoesNotContain("style=", markup);
    }

    [Fact]
    public void P2_the_controller_decides_each_screen_from_the_keys()
    {
        static PreOrderPageViewModel M(IActionResult r) => Assert.IsType<PreOrderPageViewModel>(Assert.IsType<ViewResult>(r).Model);
        Assert.False(M(Arrange([]).Controller.Index()).CanRead);
        Assert.False(M(Arrange(["crm.visit-notifications.read", "commercial.price-lists.read"]).Controller.Index()).CanRead);
        Assert.True(M(Arrange([Read]).Controller.Index()).CanRead);
        Assert.True(M(Arrange([ReadAll]).Controller.Index()).CanRead);
        var rep = M(Arrange([Read, Create, PreOrdersController.Submit]).Controller.Index());
        Assert.Equal((true, true, false, false, false), (rep.CanCreate, rep.CanSubmit, rep.CanApprove, rep.SeesTeam, rep.CanRecordDepotResponse));
        var manager = M(Arrange([Read, Approve]).Controller.ApprovePage(Order));
        Assert.Equal((true, true, Order, "approve"), (manager.CanApprove, manager.SeesTeam, manager.OrderId!.Value, manager.Screen));
        Assert.False(M(Arrange([Read]).Controller.CreatePage(null)).CanCreate);
        var link = M(Arrange([Approve]).Controller.ApprovalLink("  tok-1  "));
        Assert.Equal(("link", "tok-1"), (link.Screen, link.LinkToken));
        var settings = Assert.IsType<PreOrderSettingsViewModel>(Assert.IsType<ViewResult>(ArrangeSettings(["commercial.pre-order-settings.read"]).Controller.Index()).Model);
        Assert.Equal((true, false), (settings.CanRead, settings.CanUpdate));
        Assert.False(Assert.IsType<PreOrderSettingsViewModel>(Assert.IsType<ViewResult>(ArrangeSettings([Read]).Controller.Index()).Model).CanRead);

        // the settings form has a save button only with the update key; every field is disabled without it
        var form = View("PreOrderSettings/Index.cshtml");
        var save = form.IndexOf("id=\"po-settings-save\"", StringComparison.Ordinal);
        Assert.True(form.LastIndexOf("@if (Model.CanUpdate)", save, StringComparison.Ordinal) > 0);
        Assert.Equal(4, Regex.Matches(form, Regex.Escape("@(readOnly ? \"disabled\" : \"\")")).Count);
    }

    [Fact]
    public void P3_the_list_is_the_shared_v2_shell_in_server_mode_with_the_standard_filter_header_and_quick_view()
    {
        var table = View("PreOrders/_DataTable.cshtml");
        Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=\"listShell\" />", table);
        Assert.Contains("TableId = \"dt-preorders\"", table);
        Assert.Contains("DataMode = \"server\"", table);
        Assert.Contains("HasSelection = false", table);

        var view = View("PreOrders/Index.cshtml");
        foreach (var marker in new[] { "<partial name=\"~/Views/Commercial/PreOrders/_Filter.cshtml\" model=\"Model\" />", "<partial name=\"~/Views/Commercial/PreOrders/_DataTable.cshtml\" model=\"Model\" />",
                     "<partial name=\"~/Views/Commercial/PreOrders/_IndexL10n.cshtml\" />", "<partial name=\"~/Views/Commercial/_CommercialRulesL10n.cshtml\" />", "id=\"offcanvasDetailsPreview\"",
                     "<h5 class=\"mb-0\">@Localizer[\"PageTitle\"]</h5>", "<p class=\"mb-0 text-muted\">@Localizer[\"PageDescription\"]</p>" })
        {
            Assert.Contains(marker, view);
        }

        var filter = View("PreOrders/_Filter.cshtml");
        foreach (var chip in new[] { "filterPeriod", "filterStatus", "filterRep", "filterPharmacy", "filterDepot", "filterSku", "filterCountry", "filterAwaiting" })
        {
            Assert.Matches("<select id=\"" + chip + "\" class=\"form-select form-select-sm select2\" data-placeholder=", filter);
        }

        // the rep chip only for a caller who sees more than their own; "Onayımı bekleyenler" only for an approver
        Assert.True(filter.LastIndexOf("@if (Model.SeesTeam)", filter.IndexOf("filterRep", StringComparison.Ordinal), StringComparison.Ordinal) > 0);
        Assert.True(filter.LastIndexOf("@if (Model.CanApprove)", filter.IndexOf("filterAwaiting", StringComparison.Ordinal), StringComparison.Ordinal) > 0);

        var js = Script("index.js");
        Assert.Contains("list = await window.DitenDataTable.createList({", js);
        Assert.Contains("const endpoint = '/Commercial/PreOrders/api';", js);
        Assert.Contains("export: { mode: 'server', url: endpoint + '/list/export'", js);
        Assert.Contains("{ targets: '_all', orderable: false }", js);                          // no sort across currencies (AC-UI-2)
        Assert.Contains("exportPermitted: flag('can-export')", js);
        var code = Regex.Replace(Regex.Replace(js, @"/\*[\s\S]*?\*/", ""), @"(^|[^:])//.*$", "$1", RegexOptions.Multiline);
        Assert.DoesNotContain("serverSide", code);
        Assert.DoesNotMatch(@"pageSize=\d", code);

        // the layout primary and the semantic tokens, never a fixed colour; logical sides only (RTL)
        var css = Script("preorders.css");
        var cssCode = Regex.Replace(css, @"/\*[\s\S]*?\*/", "");
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", cssCode);
        Assert.DoesNotMatch(@"rgba?\(\s*\d", cssCode);
        Assert.Contains("var(--bs-primary)", css);
        Assert.DoesNotMatch(@"(margin|padding|border)-(left|right)\b|(^|[\s;{])(left|right)\s*:", cssCode);
    }

    // ═══ 2 · the proxy: the key HERE first, then the CRM path ══════════════════════════════════════════════════

    [Fact]
    public async Task P4_every_write_and_read_checks_its_key_before_the_crm_is_asked()
    {
        // without the key: 403 and NO gateway call
        async Task Refused(string[] keys, Func<PreOrdersController, Task<IActionResult>> call)
        {
            var (gw, c) = Arrange(keys, "{}");
            Assert.Equal(403, Status(await call(c)));
            Assert.Empty(gw.Calls);
        }

        await Refused([], c => c.List(0, 10, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        await Refused([Read], c => c.ExportList("csv", null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        await Refused([Read], c => c.Context(CancellationToken.None));
        await Refused([Read], c => c.Preview(CancellationToken.None));
        await Refused([Read], c => c.CreateDraft(CancellationToken.None));
        await Refused([Read], c => c.UpdateDraft(Order, CancellationToken.None));
        await Refused([Create], c => c.Order(Order, CancellationToken.None));
        await Refused([Read], c => c.PharmacyHistory(Order, CancellationToken.None));
        await Refused([Create], c => c.DocumentPdf(Order, "depot", false, CancellationToken.None));
        await Refused([Read], c => c.ResolveLink(CancellationToken.None));
        await Refused([], c => c.Skus("amok", CancellationToken.None));
        foreach (var (action, key) in PreOrdersController.ActionKeys)
        {
            var others = new[] { Read, ReadAll, Create, PreOrdersController.Submit, Approve, PreOrdersController.Cancel, PreOrdersController.RecordDepotResponse,
                PreOrdersController.RetryTransmission, PreOrdersController.Export }.Where(k => k != key).ToArray();
            await Refused(others, c => c.OrderAction(Order, action, CancellationToken.None));
        }

        // the table is MOD-0168 §14
        Assert.Equal(new Dictionary<string, string>
        {
            ["discard"] = Create, ["signature"] = Create, ["copy"] = Create, ["submit"] = "commercial.pre-orders.submit", ["decision"] = Approve,
            ["cancel"] = "commercial.pre-orders.cancel", ["retry-transmission"] = "commercial.pre-orders.retry-transmission",
            ["depot-response"] = "commercial.pre-orders.record-depot-response"
        }.OrderBy(x => x.Key), PreOrdersController.ActionKeys.OrderBy(x => x.Key));

        // an unknown action is 404, never forwarded
        var (g404, c404) = Arrange(PreOrdersController.ActionKeys.Values.Distinct().ToArray(), "{}");
        Assert.IsType<NotFoundResult>(await c404.OrderAction(Order, "delete", CancellationToken.None));
        Assert.Empty(g404.Calls);

        // the settings save: .update checked here
        var (gs, cs) = ArrangeSettings(["commercial.pre-order-settings.read"], "{}");
        Assert.Equal(403, Status(await cs.Put(CancellationToken.None)));
        Assert.Empty(gs.Calls);
    }

    [Fact]
    public async Task P5_with_its_key_each_call_goes_to_its_crm_path_with_the_body_and_never_with_a_tenant_id()
    {
        async Task<(string Method, string Path, string Body)> One(string[] keys, Func<PreOrdersController, Task<IActionResult>> call, string? body = null)
        {
            var (gw, c) = Arrange(keys, "{\"data\":{}}", body);
            Assert.Equal(200, Status(await call(c)));
            return Assert.Single(gw.Calls);
        }

        Assert.Equal(("GET", "/api/commercial/pre-orders/context"), Take(await One([Create], c => c.Context(CancellationToken.None))));
        var preview = await One([Create], c => c.Preview(CancellationToken.None), "{\"pharmacyAccountId\":\"" + OrderId + "\",\"lines\":[]}");
        Assert.Equal(("POST", "/api/commercial/pre-orders/preview"), Take(preview));
        Assert.Contains("pharmacyAccountId", preview.Body);
        Assert.Equal(("PUT", "/api/commercial/pre-orders/" + OrderId), Take(await One([Create], c => c.UpdateDraft(Order, CancellationToken.None), "{\"expectedVersion\":3}")));
        foreach (var (action, key) in PreOrdersController.ActionKeys)
        {
            Assert.Equal(("POST", "/api/commercial/pre-orders/" + OrderId + "/" + action), Take(await One([key], c => c.OrderAction(Order, action, CancellationToken.None), "{\"expectedVersion\":1}")));
        }

        Assert.Equal(("GET", "/api/commercial/pre-orders/" + OrderId + "/pharmacy-history"), Take(await One([Approve], c => c.PharmacyHistory(Order, CancellationToken.None))));
        Assert.Equal(("GET", "/api/commercial/pre-order-approval-links/a%2Fb%3Dc"), Take(await One([Approve], c => c.ResolveLink(CancellationToken.None), "{\"token\":\"a/b=c\"}")));

        // a body that names tenantId is refused (TenantId is server-resolved)
        var (gt, ct) = Arrange([Create], "{}", "{\"tenantId\":\"x\",\"lines\":[]}");
        Assert.Equal(400, Status(await ct.CreateDraft(CancellationToken.None)));
        Assert.Empty(gt.Calls);
        // no token, no call
        var (gn, cn) = Arrange([Approve], "{}", "{}");
        Assert.Equal(400, Status(await cn.ResolveLink(CancellationToken.None)));
        Assert.Empty(gn.Calls);
    }

    [Fact]
    public async Task P6_the_list_contract_becomes_the_crm_query_rows_pass_an_allow_list_and_the_refusals_ask_nothing()
    {
        const string page = "{\"data\":{\"items\":[{\"id\":\"" + OrderId + "\",\"code\":\"MG-ORD-000001\",\"status\":\"pending_approval\",\"pharmacyName\":\"Ecz. Deniz\","
            + "\"pharmacyCityRef\":\"TR-34-ISTANBUL\",\"repUserId\":\"aaaaaaaa-0000-0000-0000-000000000001\",\"priceMode\":\"priced\",\"currency\":\"TRY\",\"gross\":1234.5,"
            + "\"pharmacyEmail\":\"ecz@x.com\",\"signature\":{\"binaryRef\":\"x\"}}],\"total\":31,\"page\":3,\"pageSize\":10}}";
        var (gw, c) = Arrange([Read], page);
        var result = await c.List(20, 10, null, null, null, "custom", "2026-10-01", "2026-10-21", "pending_approval", null, OrderId, null, null, "tr", "true", CancellationToken.None);
        Assert.Equal(200, Status(result));
        var first = gw.Calls[0].Path;
        Assert.StartsWith("/api/commercial/pre-orders?", first);
        foreach (var part in new[] { "from=2026-10-01", "to=2026-10-21", "status=pending_approval", "pharmacyAccountId=" + OrderId, "countryCode=TR", "awaitingMe=true", "page=3", "pageSize=10" })
        {
            Assert.Contains(part, first);
        }

        Assert.Contains(gw.Calls, x => x.Path.Contains("pageSize=1", StringComparison.Ordinal) && !x.Path.Contains("status=", StringComparison.Ordinal)); // the unfiltered total
        var row = Json(Assert.IsType<OkObjectResult>(result).Value).GetProperty("data").GetProperty("items")[0];
        Assert.False(row.TryGetProperty("pharmacyEmail", out _));
        Assert.False(row.TryGetProperty("signature", out _));
        Assert.False(row.TryGetProperty("pharmacyCityRef", out _));
        Assert.Equal("Istanbul", row.GetProperty("pharmacyCity").GetString());                        // no city set wired → the readable name

        // refused before any call: a column order (across currencies), an unknown status, a non-id, a bad range
        foreach (var call in new Func<PreOrdersController, Task<IActionResult>>[]
                 {
                     x => x.List(0, 10, null, "gross", "desc", null, null, null, null, null, null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, null, null, null, "sent", null, null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, null, null, null, null, "rep-1", null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None),
                     x => x.List(0, 10, null, null, null, "custom", "2026-10-21", "2026-10-01", null, null, null, null, null, null, null, CancellationToken.None)
                 })
        {
            var (g, ctl) = Arrange([Read], page);
            Assert.Equal(400, Status(await call(ctl)));
            Assert.Empty(g.Calls);
        }

        // the period twin (the CRM's creation day): "" = every order
        Assert.Equal((null, null, null), PreOrdersController.ResolveRange("", null, null, DateOnly.FromDateTime(Today.UtcDateTime)));
        Assert.Equal(("2026-10-15", "2026-10-21", (string?)null), PreOrdersController.ResolveRange("last7", null, null, DateOnly.FromDateTime(Today.UtcDateTime)));
        Assert.Equal(("2026-10-01", "2026-10-21", (string?)null), PreOrdersController.ResolveRange("month", null, null, DateOnly.FromDateTime(Today.UtcDateTime)));
    }

    [Fact]
    public async Task P7_the_export_is_the_screen_behind_the_export_key()
    {
        string Rows(int count, int total) => "{\"data\":{\"items\":[" + string.Join(",", Enumerable.Range(0, count).Select(i => "{\"id\":\"" + Guid.NewGuid() + "\",\"code\":\"MG-ORD-" + i.ToString("000000") + "\",\"status\":\"transmitted\",\"priceMode\":\"quantity\",\"lineCount\":2,\"createdAt\":\"2026-10-20T08:00:00Z\"}")) + "],\"total\":" + total + "}}";
        var (gw, c) = Arrange([Read, PreOrdersController.Export], Rows(1, 1), acceptLanguage: "tr");
        var file = Assert.IsType<FileContentResult>(await c.ExportList("csv", null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Contains("MG-ORD-000000", Encoding.UTF8.GetString(file.FileContents));
        Assert.All(gw.Calls, x => Assert.Contains("pageSize=100", x.Path));

        var (gw2, c2) = Arrange([Read, PreOrdersController.Export], Rows(1, 6000));
        Assert.Equal(413, Status(await c2.ExportList("xlsx", null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None)));
        Assert.Single(gw2.Calls);
    }

    [Fact]
    public async Task P8_screen_e_has_the_two_copies_and_the_document_endpoint_checks_the_copy_before_the_crm()
    {
        var view = View("PreOrders/Document.cshtml");
        Assert.Contains("id=\"po-copy-depot\" value=\"depot\"", view);
        Assert.Contains("id=\"po-copy-pharmacy\" value=\"pharmacy\"", view);
        Assert.Matches(@"class=""btn-group"" role=""group""", view);
        Assert.Contains("id=\"po-trail\"", view);
        Assert.Equal(["depot", "pharmacy"], PreOrdersController.Copies);

        var (gw, c) = Arrange([Read], "%PDF");
        Assert.Equal(400, Status(await c.DocumentPdf(Order, "original", false, CancellationToken.None)));
        Assert.Empty(gw.Calls);
        var inline = Assert.IsType<FileContentResult>(await c.DocumentPdf(Order, null, false, CancellationToken.None));
        Assert.Equal("application/pdf", inline.ContentType);
        Assert.Equal(string.Empty, inline.FileDownloadName);                                          // inline: the viewer
        Assert.Equal("/api/commercial/pre-orders/" + OrderId + "/document?copy=depot", gw.Calls[^1].Path);
        var download = Assert.IsType<FileContentResult>(await c.DocumentPdf(Order, "PHARMACY", true, CancellationToken.None));
        Assert.Equal("/api/commercial/pre-orders/" + OrderId + "/document?copy=pharmacy", gw.Calls[^1].Path);
        Assert.Equal("MG-ORD-000001-pharmacy.pdf", download.FileDownloadName);

        // the page reads the same two urls (the twin of DocumentPdf)
        var node = RunNode(Ui() + "globalThis.document = { getElementById: () => null, addEventListener: () => {} }; const D = (function(){ const window = globalThis; " + Script("document.js").Replace("'use strict';", "") + " return PreOrderDocument; })();"
            + "process.stdout.write(JSON.stringify([D.pdfUrl('" + OrderId + "', 'depot', false), D.pdfUrl('" + OrderId + "', 'pharmacy', true), D.pdfUrl('" + OrderId + "', 'x', false)]));");
        if (node is null) return;
        Assert.Equal(new[] { "/Commercial/PreOrders/api/orders/" + OrderId + "/document?copy=depot", "/Commercial/PreOrders/api/orders/" + OrderId + "/document?copy=pharmacy&download=true",
            "/Commercial/PreOrders/api/orders/" + OrderId + "/document?copy=depot" }, JsonSerializer.Deserialize<string[]>(node));
    }

    [Fact]
    public void P9_the_depot_answer_exists_only_for_sales_operations()
    {
        var view = View("PreOrders/Index.cshtml");
        foreach (var piece in new[] { "<form id=\"po-depot-form\"", "id=\"po-dr-save\"" })
        {
            var at = view.IndexOf(piece, StringComparison.Ordinal);
            Assert.True(at > 0, piece);
            var guard = view.LastIndexOf("@if (Model.CanRecordDepotResponse)", at, StringComparison.Ordinal);
            Assert.True(guard > 0, piece + " is not inside the sales operations guard");
            Assert.DoesNotContain("}", view[(view.IndexOf('{', guard) + 1)..at]);
        }

        Assert.Contains("data-dt-primary=\"chord\">@Localizer[\"DepotResponseSave\"]", view);          // a record write: Ctrl / ⌘ + Enter (UXH-001)
        // the drawer draws it only for an order the CRM says may take it (can.recordDepotResponse) and that was transmitted
        Assert.Contains("const show = !!form && !!(o && o.can && o.can.recordDepotResponse) && o.status === 'transmitted';", Script("index.js"));
        Assert.Equal("commercial.pre-orders.record-depot-response", PreOrdersController.ActionKeys["depot-response"]);
    }

    // ═══ 3 · the link paths are the CRM's constants ═════════════════════════════════════════════════════════════

    [Fact]
    public void P10_the_e_mail_and_work_center_link_paths_are_the_crm_constants_and_the_web_routes_answer_them()
    {
        var repo = Repo();
        var links = File.ReadAllText(Path.Combine(repo, "services", "Diten.CrmService", "src", "Diten.CrmService.Infrastructure", "Commercial", "PreOrderClients.cs"));
        var ports = File.ReadAllText(Path.Combine(repo, "services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "Commercial", "PreOrders", "PreOrderPorts.cs"));
        // W6-FIX-a (G-13) — the builders take the tenant too (the link names it); the path they start with is unchanged
        Assert.Equal(PreOrdersController.DetailsPath, Regex.Match(links, @"OrderUrl\(Guid tenantId, Guid preOrderId\)\s*=> _base \+ ""([^""]+)""").Groups[1].Value);
        Assert.Equal(PreOrdersController.ApprovalLinkPath, Regex.Match(links, @"ApprovalLinkUrl\(Guid tenantId, string token\)\s*=> _base \+ ""([^""]+)""").Groups[1].Value);
        Assert.Equal(PreOrdersController.ApprovePath, Regex.Match(ports, @"Link\(Guid id\) => \$""([^{]+)\{id:D\}""").Groups[1].Value);

        Assert.Equal("/Commercial/PreOrders/Details/", PreOrdersController.DetailsPath);
        Assert.Equal("/Commercial/PreOrders/ApprovalLink?token=", PreOrdersController.ApprovalLinkPath);
        Assert.Equal("/Commercial/PreOrders/Approve/", PreOrdersController.ApprovePath);

        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "Commercial", "PreOrdersController.cs"));
        foreach (var route in new[] { "[Route(\"Commercial/PreOrders\")]", "[HttpGet(\"Details/{id:guid}\")]", "[HttpGet(\"ApprovalLink\")]", "[HttpGet(\"Approve/{id:guid}\")]",
                     "public IActionResult ApprovalLink([FromQuery] string? token)" })
        {
            Assert.Contains(route, controller);
        }

        // the link page is [Authorize] (D4: signed in first) — the class attribute covers every action
        Assert.Matches(@"\[Authorize\]\s*\[Route\(""Commercial/PreOrders""\)\]", controller);
        Assert.DoesNotContain("AllowAnonymous", controller);
    }

    // ═══ 4 · the shared rule messages cover every CRM code ════════════════════════════════════════════════════

    [Fact]
    public void P11_every_mod0156_reason_code_and_mod0168_refusal_code_has_its_message_in_seven_languages_with_the_contract_placeholders()
    {
        var repo = Repo();
        var pricing = File.ReadAllText(Path.Combine(repo, "services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "Commercial", "Pricing", "PricingReasonCodes.cs"));
        var preOrders = File.ReadAllText(Path.Combine(repo, "services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "Commercial", "PreOrders", "PreOrderReasonCodes.cs"));
        var contract = File.ReadAllText(Path.Combine(repo, "services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "Commercial", "Pricing", "PricingHistory", "PricingHistoryFeature.cs"));
        static List<string> Consts(string source, string type)
        {
            var body = Regex.Match(source, @"public static class " + type + @"\s*\{([\s\S]*?)\n\}").Groups[1].Value;
            return Regex.Matches(body, @"public const string \w+ = ""([^""]+)"";").Select(m => m.Groups[1].Value).ToList();
        }

        var reasons = Consts(pricing, "PricingReasonCodes");
        var refusals = Consts(preOrders, "PreOrderErrors");
        Assert.Equal(12, reasons.Count);
        Assert.True(refusals.Count >= 38, "the PreOrderErrors constants were not read");
        // W6-WEB-b: the MOD-0156 refusal codes joined the ONE shared source (screen D words them from it too)
        var pricingRefusals = Consts(pricing, "PricingErrors");
        Assert.True(pricingRefusals.Count >= 26, "the PricingErrors constants were not read");

        // the contract's parameters per reason code (MOD-0156 §4.6.3, the pricing-contract endpoint's own list)
        var parameters = Regex.Matches(contract, @"Code\(PricingReasonCodes\.(\w+), PricingFindingSeverities\.\w+((?:, ""\w+"")*)\)")
            .ToDictionary(m => Regex.Match(pricing, @"public const string " + m.Groups[1].Value + @" = ""([^""]+)""").Groups[1].Value,
                m => Regex.Matches(m.Groups[2].Value, @"""(\w+)""").Select(p => p.Groups[1].Value).Order().ToList());
        Assert.Equal(reasons.Order(), parameters.Keys.Order());

        var english = Rules("en");
        Assert.Equal(english.OrderBy(x => x.Key), Rules("").OrderBy(x => x.Key));
        var expected = reasons.Concat(refusals).Concat(pricingRefusals).Select(c => CommercialRules.Prefix + c).Concat(CommercialRules.Outcomes.Select(o => CommercialRules.Prefix + "Outcome." + o)).ToList();
        foreach (var language in Languages)
        {
            var words = Rules(language);
            Assert.Equal(expected.Order(), words.Keys.Order());                                       // exactly the CRM's codes: none missing, none invented
            foreach (var code in reasons)
            {
                var text = words[CommercialRules.Prefix + code];
                Assert.Equal(parameters[code], Regex.Matches(text, @"\{(\w+)\}").Select(m => m.Groups[1].Value).Distinct().Order().ToList());
            }

            foreach (var key in expected)
            {
                Assert.False(string.IsNullOrWhiteSpace(words[key]), language + " " + key);
                if (language != "en") Assert.NotEqual(english[key], words[key]);
            }
        }

        // the bridge: every entry of the resource travels (the resource is the key list) and merges into window.L10n
        var partial = View("_CommercialRulesL10n.cshtml");
        Assert.Contains("Rules.GetAllStrings(includeParentCultures: true)", partial);
        Assert.Contains("<script id=\"@CommercialRules.PayloadId\" type=\"application/json\">", partial);
        Assert.Contains("getElementById('commercial-rules-l10n')", Common("commercial-rules.js"));
        Assert.Equal("commercial-rules-l10n", CommercialRules.PayloadId);
        // and every pre-order page loads it
        foreach (var page in new[] { "Index", "Create", "Edit", "Details", "Approve", "ApprovalLink", "Document" })
        {
            Assert.Contains("<partial name=\"~/Views/Commercial/_CommercialRulesL10n.cshtml\" />", View("PreOrders/" + page + ".cshtml"));
        }
    }

    [Fact]
    public void P12_a_finding_is_worded_in_the_culture_with_its_parameters_and_an_unknown_code_has_no_words()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        var tr = Rules("tr");
        var ar = Rules("ar");
        string Run(Dictionary<string, string> words, string lang, string expr)
            => RunNode("globalThis.L10n = " + JsonSerializer.Serialize(words) + "; globalThis.document = { documentElement: { lang: '" + lang + "' }, getElementById: () => null };"
                + "const R = require('" + Common("commercial-rules.js", path: true) + "'); process.stdout.write(JSON.stringify(" + expr + "));")!;
        const string fsi = "⁨";
        const string pdi = "⁩";
        Assert.Equal("Yönetici onayı gerekir: iskonto %" + fsi + "12,5" + pdi + " > sınır %" + fsi + "10" + pdi,
            JsonSerializer.Deserialize<string>(Run(tr, "tr", "R.text({ code: 'discount.soft-exceeded', params: { value: '12.5', limit: '10' } })")));
        Assert.Equal("Girilemez: " + fsi + "Tutukon 50mg" + pdi + " Kasım fiyat listesinde yok".Replace("Kasım", fsi + "Kasım" + pdi),
            JsonSerializer.Deserialize<string>(Run(tr, "tr", "R.text({ code: 'price.sku-not-in-list', params: { skuId: 'x', priceListName: 'Kasım' } }, { skuName: () => 'Tutukon 50mg' })")));
        Assert.Contains("<bdi>10+2</bdi>", JsonSerializer.Deserialize<string>(Run(ar, "ar", "R.html({ code: 'mf.soft-exceeded', params: { value: '10+3', valuePct: '23.1', limit: '10+2', limitPct: '16.7' } })")));
        Assert.Contains("<bdi>&lt;b&gt;</bdi>", JsonSerializer.Deserialize<string>(Run(tr, "tr", "R.html({ code: 'price.sku-not-in-list', params: { skuId: 'x', priceListName: '<b>' } })")));
        Assert.Equal("", JsonSerializer.Deserialize<string>(Run(tr, "tr", "R.text({ code: 'no.such-code', params: {} })")));
        Assert.Equal(tr["Rule.Outcome.hard"], JsonSerializer.Deserialize<string>(Run(tr, "tr", "R.outcome('hard')")));
        Assert.Equal(tr["Rule.pre_order_already_decided"], JsonSerializer.Deserialize<string>(Run(tr, "tr", "R.error('pre_order_already_decided')")));
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(Run(tr, "tr", "R.error('pre_order_no_such')")).RootElement.ValueKind);
    }

    // ═══ 5 · quantity mode, send blockers, the page rules (Node) ═════════════════════════════════════════════════

    [Fact]
    public void P13_in_quantity_mode_no_price_or_discount_field_exists_and_no_discount_travels()
    {
        // the form markup has no static price / discount input: the line inputs are drawn per mode
        var form = View("PreOrders/_Form.cshtml");
        Assert.DoesNotMatch(@"po-disc(?!ard)", form);
        Assert.DoesNotContain("UnitPrice", form);
        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Ui() + "globalThis.L10n = {};"
            + "const q = U.formLineHtml({ key: 'l1', skuId: 'a', skuText: 'Tutukon', quantity: 5, mf: '10+2', discount: '12' }, false, false);"
            + "const p = U.formLineHtml({ key: 'l1', skuId: 'a', skuText: 'Tutukon', quantity: 5, mf: '10+2', discount: '12' }, true, false);"
            + "const rq = U.lineResult({ unitPrice: 10, netAmount: 50, outcome: 'ok', findings: [], freeQuantity: 0 }, { priced: false, currency: '' }) + JSON.stringify(U.lineCells({ unitPrice: 10, netAmount: 50 }, { priced: false, currency: '' }));"
            + "const rp = JSON.stringify(U.lineCells({ unitPrice: 10, netAmount: 50, outcome: 'ok', findings: [], freeQuantity: 0 }, { priced: true, currency: 'TRY' }));"
            + "const lq = C.linesInput([{ skuId: 'a', quantity: '5', mf: '10+2', discount: '12' }], false);"
            + "const lp = C.linesInput([{ skuId: 'a', quantity: '5', mf: '10+2', discount: '12,5' }], true);"
            + "process.stdout.write(JSON.stringify({ qDisc: q.includes('po-disc'), pDisc: p.includes('po-disc'), qInputs: (q.match(/<input/g) || []).length, rqPrice: rq.includes('TRY') || rq.includes('bx-lock-alt'), rpPrice: rp.includes('TRY'), lq: lq, lp: lp,"
            + " total: U.totals({ priceMode: 'quantity' }), locked: U.formLineHtml({ key: 'l2' }, true, true).includes('po-remove') }));")!;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.False(r.GetProperty("qDisc").GetBoolean());
        Assert.True(r.GetProperty("pDisc").GetBoolean());
        Assert.Equal(2, r.GetProperty("qInputs").GetInt32());                                         // quantity + MF only
        Assert.False(r.GetProperty("rqPrice").GetBoolean());
        Assert.True(r.GetProperty("rpPrice").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("lq")[0].GetProperty("discountPct").ValueKind);   // AC-PRICE-3: never sent
        Assert.Equal(12.5m, r.GetProperty("lp")[0].GetProperty("discountPct").GetDecimal());
        Assert.Equal(10, r.GetProperty("lq")[0].GetProperty("mf").GetProperty("buy").GetInt32());
        Assert.Contains("alert-info", r.GetProperty("total").GetString());                             // the "depot applies the price" note, no amount
        Assert.False(r.GetProperty("locked").GetBoolean());                                            // a locked line cannot be removed
    }

    [Fact]
    public void P14_the_page_rules_send_lock_limits_history_link_and_period()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Ui() + @"
const out = {};
out.send = [
  C.sendBlockers({ canSubmit: true, lineCount: 1, outcome: 'ok', hasDepot: true, hasSignature: true, dirty: false }),
  C.sendBlockers({ canSubmit: true, lineCount: 0, outcome: 'hard', hasDepot: false, hasSignature: false, dirty: true }),
  C.sendBlockers({ canSubmit: false, lineCount: 1, outcome: 'soft', hasDepot: true, hasSignature: true, dirty: false })];
out.lock = ['draft', 'pending_approval', 'approved', 'transmitted', 'transmission_failed', 'rejected', 'cancelled']
  .map((s) => C.lockState({ status: s, can: { edit: s === 'draft', cancel: ['approved', 'transmitted', 'transmission_failed'].includes(s), copy: ['rejected', 'cancelled'].includes(s) } }));
out.mf = [C.parseMf('10+2'), C.parseMf(' 5 + 1 '), C.parseMf('10+0'), C.parseMf('abc'), C.parseMf('')];
out.disc = [C.parseDiscount('12,5'), C.parseDiscount('99.9'), C.parseDiscount('100'), C.parseDiscount('1.25'), C.parseDiscount('')];
out.limits = C.limitCells({ findings: [
  { code: 'discount.soft-exceeded', severity: 'soft', params: { value: '12', limit: '10' } },
  { code: 'discount.hard-exceeded', severity: 'hard', params: { value: '12', limit: '11' } },
  { code: 'quantity.below-min', severity: 'hard', params: { value: '2', min: '5' } },
  { code: 'mf.soft-exceeded', severity: 'soft', params: { value: '10+3', valuePct: '23.1', limit: '10+2', limitPct: '16.7' } }] });

out.links = ['valid', 'expired', 'consumed', 'superseded', 'odd', null].map(C.linkStateKey);
out.hours = C.hoursLeft('2026-10-23T09:00:00Z', Date.parse('2026-10-21T09:30:00Z'));
out.range = [C.rangeOf('', '2026-10-21'), C.rangeOf('last30', '2026-10-21'), C.rangeOf('custom', '2026-10-21', '2026-10-09', '2026-10-01')];
out.box = [C.approvalBox(null, null), C.approvalBox({ approvalRequired: true, approvalReasons: ['tenant-setting', 'soft-exceeded'] }, { approver: { available: true, resolution: 'ManagerChain', managers: [{ displayName: 'Mehmet Kaya' }] } })];
out.why = C.whyOnMe({ approvalRequirement: { reasons: ['soft-exceeded'] }, lines: [{ lineNo: 1, outcome: 'soft', sku: { name: 'Tutukon' }, findings: [{ code: 'discount.soft-exceeded', severity: 'soft', params: {} }] }, { lineNo: 2, outcome: 'ok', findings: [] }] });
out.timeline = C.timeline({ events: [{ at: '2026-10-21T10:00:00Z', type: 'changes-requested', payload: { reason: 'MF yüksek' } }, { at: '2026-10-21T09:00:00Z', type: 'submitted' }, { at: '2026-10-21T11:00:00Z', type: 'mystery' }] });
out.err = [C.errorCode({ errors: ['pre_order_hard_limit', 'msg'] }), C.errorCode({ errorCodes: [{ code: 'EXPORT_TOO_LARGE' }] }), C.errorCode(null)];
process.stdout.write(JSON.stringify(out));")!;
        var r = JsonDocument.Parse(node).RootElement;
        static string[] Arr(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Empty(Arr(r.GetProperty("send")[0]));
        Assert.Equal(["Why_no_lines", "Why_hard", "Why_no_depot", "Why_no_signature", "Why_unsaved"], Arr(r.GetProperty("send")[1]));
        Assert.Equal(["Why_no_permission"], Arr(r.GetProperty("send")[2]));                          // a soft limit does not block (it goes to approval)

        var lk = r.GetProperty("lock");
        Assert.False(lk[0].GetProperty("locked").GetBoolean());
        Assert.Equal("Lock_pending", lk[1].GetProperty("noteKey").GetString());                       // pending approval cannot be edited
        Assert.Equal("cancel-new", lk[2].GetProperty("action").GetString());
        Assert.Equal("cancel-new", lk[4].GetProperty("action").GetString());
        Assert.Equal("copy", lk[5].GetProperty("action").GetString());                                // D6
        Assert.Equal("copy", lk[6].GetProperty("action").GetString());

        Assert.Equal(10, r.GetProperty("mf")[0].GetProperty("buy").GetInt32());
        Assert.Equal(1, r.GetProperty("mf")[1].GetProperty("free").GetInt32());
        Assert.Equal("invalid", r.GetProperty("mf")[2].GetString());
        Assert.Equal("invalid", r.GetProperty("mf")[3].GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("mf")[4].ValueKind);
        Assert.Equal(12.5m, r.GetProperty("disc")[0].GetDecimal());
        Assert.Equal(99.9m, r.GetProperty("disc")[1].GetDecimal());
        Assert.Equal("invalid", r.GetProperty("disc")[2].GetString());
        Assert.Equal("invalid", r.GetProperty("disc")[3].GetString());

        var lim = r.GetProperty("limits");
        Assert.Equal(("hard", "11"), (lim.GetProperty("discount").GetProperty("severity").GetString(), lim.GetProperty("discount").GetProperty("limit").GetString())); // the worse finding speaks
        Assert.Equal("5", lim.GetProperty("quantity").GetProperty("limit").GetString());
        Assert.Equal(("10+2", "16.7"), (lim.GetProperty("mf").GetProperty("limit").GetString(), lim.GetProperty("mf").GetProperty("limitPct").GetString()));

        // (the six months moved to the CRM in W6-FIX-d — P25; the page has no bucketing of its own any more)

        Assert.Equal(["Link_valid", "Link_expired", "Link_consumed", "Link_superseded", "Link_notfound", "Link_notfound"], Arr(r.GetProperty("links")));
        Assert.Equal(48, r.GetProperty("hours").GetInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("range")[0].GetProperty("from").ValueKind);
        Assert.Equal("2026-09-22", r.GetProperty("range")[1].GetProperty("from").GetString());
        Assert.Equal("ErrRangeOrder", r.GetProperty("range")[2].GetProperty("error").GetString());
        Assert.Equal("Need_unknown", r.GetProperty("box")[0].GetProperty("answerKey").GetString());
        Assert.Equal(["Need_tenant_setting", "Need_soft"], Arr(r.GetProperty("box")[1].GetProperty("reasons")));
        Assert.Equal("Approver_chain", r.GetProperty("box")[1].GetProperty("approver").GetProperty("key").GetString());  // the vacant seat's fallback
        Assert.Equal(1, r.GetProperty("why")[0].GetProperty("lines").GetArrayLength());
        var tl = r.GetProperty("timeline");
        Assert.Equal(["Ev_submitted", "Ev_changes_requested", "Ev_unknown"], tl.EnumerateArray().Select(x => x.GetProperty("key").GetString()!).ToArray());
        Assert.Equal("MF yüksek", tl[1].GetProperty("reason").GetString());
        Assert.Equal("pre_order_hard_limit", r.GetProperty("err")[0].GetString());
        Assert.Equal("EXPORT_TOO_LARGE", r.GetProperty("err")[1].GetString());

        // the twin of the server's period: the same windows (C# above)
        Assert.Equal("2026-09-22", PreOrdersController.ResolveRange("last30", null, null, new DateOnly(2026, 10, 21)).From);

        // every timeline event of the CRM has its words (PreOrderEventTypes)
        var types = File.ReadAllText(Path.Combine(Repo(), "services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities", "Commercial", "PreOrder.cs"));
        var events = Regex.Matches(Regex.Match(types, @"public static class PreOrderEventTypes\s*\{([\s\S]*?)\n\}").Groups[1].Value, @"= ""([a-z-]+)"";").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(18, events.Count);
        Assert.All(events, e => Assert.Contains("Ev_" + e.Replace('-', '_'), PreOrdersIndex.ScriptKeys));
        Assert.All(PreOrdersController.Statuses, s => Assert.Contains("St_" + s, PreOrdersIndex.ScriptKeys));
    }

    [Fact]
    public void P15_the_approval_screens_the_reason_is_required_and_the_link_token_travels_with_the_decision()
    {
        var approve = Script("approve.js");
        Assert.Contains("if (decision === 'approve') {", approve);                                     // G-7: approve asks through the shared confirmation (P20)
        Assert.Contains("U.confirm(L().ApproveConfirmTitle, U.T('ApproveConfirmText', order.code || ''), L().Approve, 'success', () => send(decision, null));", approve);
        Assert.Contains("if (!reason) return;", approve);
        Assert.Contains("if (isLink()) { body.linkToken = token; }", approve);
        Assert.Contains("endpoint + '/approval-link', { token: token }", approve);                  // the token in the body
        var ui = Script("preorders-ui.js");
        Assert.Contains("ok.disabled = true;", ui);                                                   // the dialog's button closed until a reason is typed
        Assert.Contains("const onInput = () => { ok.disabled = !area.value.trim(); };", ui);
        var modal = View("PreOrders/_ReasonModal.cshtml");
        Assert.Contains("id=\"po-reason-ok\" data-dt-primary=\"chord\" disabled", modal);
        foreach (var page in new[] { "Approve", "ApprovalLink" })
        {
            var view = View("PreOrders/" + page + ".cshtml");
            Assert.Contains("ViewData[\"PageCode\"] = PreOrdersIndex.ApproveHelpCode;", view);
            foreach (var id in new[] { "po-approve", "po-reject", "po-changes", "po-why", "po-limits" }) Assert.Contains("id=\"" + id + "\"", view);
        }

        Assert.Contains("id=\"po-history\"", View("PreOrders/Approve.cshtml"));
        Assert.Contains("id=\"po-you\"", View("PreOrders/Approve.cshtml"));
        Assert.Contains("id=\"po-link-state\"", View("PreOrders/ApprovalLink.cshtml"));
    }

    // ═══ 6 · words, help, menu, routes ═════════════════════════════════════════════════════════════════════════

    private static readonly HashSet<(string Key, string Lang)> SameAsEnglish =
    [
        ("ColMf", "tr"), ("GtinShort", "tr"), ("TotalNet", "tr"), ("DownloadPdf", "tr"), ("ColDate", "fr"), ("ColTotal", "fr"), ("ColActions", "fr"),
        ("OpenDocument", "fr"), ("FactDate", "fr"), ("GtinShort", "fr"), ("TotalNet", "fr"), ("DownloadPdf", "fr"), ("ColTotal", "es"), ("GtinShort", "es"),
        ("DownloadPdf", "es"), ("GtinShort", "zh"), ("DownloadPdf", "zh"), ("GtinShort", "ar"), ("DownloadPdf", "ar"), ("GtinShort", "ru"), ("DownloadPdf", "ru")
    ];

    [Fact]
    public void P16_every_word_of_the_screens_is_in_seven_languages_and_every_word_the_scripts_name_is_a_declared_key()
    {
        var keys = PreOrdersIndex.ScriptKeys.Concat(PreOrdersIndex.ViewKeys).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        var english = Page("en");
        Assert.Equal(english.OrderBy(x => x.Key), Page("").OrderBy(x => x.Key));
        foreach (var language in Languages)
        {
            var words = Page(language);
            Assert.Equal(keys.Order(), words.Keys.Order());
            foreach (var key in keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(words[key]), language + " " + key);
                Assert.Equal(Regex.Matches(english[key], @"\{\d\}").Select(m => m.Value).Order(), Regex.Matches(words[key], @"\{\d\}").Select(m => m.Value).Order());
                if (language != "en" && english[key] == words[key]) Assert.True(SameAsEnglish.Contains((key, language)), $"{language} {key} is the English word");
            }
        }

        var scripts = string.Concat(new[] { "index.js", "form.js", "details.js", "approve.js", "document.js", "settings.js", "preorders-core.js", "preorders-ui.js" }.Select(Script));
        var named = Regex.Matches(scripts, @"L\(\)\.([A-Z][A-Za-z0-9_]+)|L\(\)\['([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'\]|\bTh?\('([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'|\b(?:key|noteKey|answerKey): '([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'|'((?:Why|Need|Lock|Approver|Err|St|Ev|DR|Link|Trail|Decided)_[A-Za-z0-9_]*[A-Za-z0-9])'")
            .Select(m => m.Groups.Values.Skip(1).First(g => g.Success).Value).Distinct().ToList();
        var shared = new[] { "Actions", "DateFormat" };
        Assert.All(named.Where(n => !shared.Contains(n)), n => Assert.Contains(n, PreOrdersIndex.ScriptKeys));
        var views = string.Concat(Directory.GetFiles(Path.Combine(WebRoot(), "Views", "Commercial", "PreOrders"), "*.cshtml").Concat(Directory.GetFiles(Path.Combine(WebRoot(), "Views", "Commercial", "PreOrderSettings"), "*.cshtml")).Select(File.ReadAllText));
        Assert.All(Regex.Matches(views, @"(?<!Shared)Localizer\[""([A-Za-z0-9_]+)""\]").Select(m => m.Groups[1].Value), k => Assert.Contains(k, keys));

        Assert.Contains("document.getElementById('preorders-l10n')", Script("index.l10n.js"));
        Assert.Contains("<script id=\"preorders-l10n\" type=\"application/json\">", View("PreOrders/_IndexL10n.cshtml"));
        Assert.Contains("foreach (var key in PreOrdersIndex.ScriptKeys)", View("PreOrders/_IndexL10n.cshtml"));
    }

    [Fact]
    public void P17_the_page_help_of_the_four_codes_is_in_seven_languages_with_its_step_icons()
    {
        foreach (var code in new[] { "PREORDERS", "COMPREORDERFORM", "COMPREORDERAPPROVE", "PREORDERSETTINGS" })
        {
            foreach (var language in Languages)
            {
                var help = Help(language);
                foreach (var key in new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, 4).SelectMany(n => new[] { "Step" + n, "Step" + n + "Title" })))
                {
                    Assert.True(help.TryGetValue($"Help.{code}.{key}", out var text) && !string.IsNullOrWhiteSpace(text), $"{language} Help.{code}.{key}");
                }

                Assert.False(help.ContainsKey($"Help.{code}.Step5"));
            }

            Assert.All(Enumerable.Range(1, 4), n => Assert.NotEqual(Diten.Web.Services.Help.PageHelpCatalog.DefaultStepIcon, Diten.Web.Services.Help.PageHelpCatalog.StepIcon(code, n)));
        }

        Assert.Equal("COMPREORDERFORM", PreOrdersIndex.FormHelpCode);
        Assert.Equal("COMPREORDERAPPROVE", PreOrdersIndex.ApproveHelpCode);
        foreach (var page in new[] { "Create", "Edit" }) Assert.Contains("ViewData[\"PageCode\"] = PreOrdersIndex.FormHelpCode;", View("PreOrders/" + page + ".cshtml"));
    }

    [Fact]
    public void P18_the_menu_pages_their_labels_in_seven_languages_and_the_gateway_routes_the_proxy_calls()
    {
        foreach (var key in new[] { "Nav.Page.PREORDERS", "Nav.Page.PREORDERSETTINGS", "Nav.Page.COMPREORDERFORM", "Nav.Page.COMPREORDERAPPROVE" })
        {
            var labels = Languages.ToDictionary(l => l, l => Assert.Single(XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{l}.resx")).Root!.Elements("data").Where(d => (string)d.Attribute("name")! == key)).Element("value")!.Value);
            Assert.Equal(7, labels.Values.Distinct().Count());
            Assert.All(labels.Values, v => Assert.False(string.IsNullOrWhiteSpace(v) || v.Contains("PREORDER", StringComparison.Ordinal)));
        }

        var repo = Repo();
        var manifest = File.ReadAllText(Path.Combine(repo, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Crm", "SelfRegistration", "CrmManifestProvider.cs"));
        Assert.Contains("new ModuleManifestPage(\"PREORDERS\", \"Pre-Orders\", \"/Commercial/PreOrders\", \"commercial.pre-orders.read\"", manifest);
        Assert.Contains("new ModuleManifestPage(\"PREORDERSETTINGS\", \"Pre-Order Settings\", \"/Commercial/PreOrderSettings\", \"commercial.pre-order-settings.read\"", manifest);

        var ocelot = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "gateway", "Diten.ApiGateway", "ocelot.json"))).RootElement.GetProperty("Routes").EnumerateArray()
            .Select(r => (Path: r.GetProperty("UpstreamPathTemplate").GetString()!, Methods: r.TryGetProperty("UpstreamHttpMethod", out var m) ? m.EnumerateArray().Select(x => x.GetString()!.ToUpperInvariant()).ToList() : []))
            .ToList();
        bool Routed(string path, string method) => ocelot.Any(r => r.Methods.Contains(method) && Regex.IsMatch(path, "^" + Regex.Replace(Regex.Escape(r.Path), @"\\\{[^}]+\}", "[^?]+") + "$", RegexOptions.IgnoreCase));
        foreach (var (method, path) in new[]
                 {
                     ("GET", "/api/commercial/pre-orders"), ("POST", "/api/commercial/pre-orders"), ("GET", "/api/commercial/pre-orders/context"),
                     ("POST", "/api/commercial/pre-orders/preview"), ("GET", "/api/commercial/pre-orders/" + OrderId), ("PUT", "/api/commercial/pre-orders/" + OrderId),
                     ("POST", "/api/commercial/pre-orders/" + OrderId + "/submit"), ("GET", "/api/commercial/pre-orders/" + OrderId + "/document"),
                     ("GET", "/api/commercial/pre-orders/" + OrderId + "/pharmacy-history"), ("GET", "/api/commercial/pre-order-settings"), ("PUT", "/api/commercial/pre-order-settings"),
                     ("GET", "/api/commercial/pre-order-approval-links/tok"), ("GET", "/api/crm/accounts"), ("GET", "/api/product-master/stockable-items"),
                     ("GET", "/api/legal-entities/lookup")
                 })
        {
            Assert.True(Routed(path, method), method + " " + path);
        }
    }

    // ═══ 7 · W6-FIX-b — the E4 findings and the mockup differences ═════════════════════════════════════════════

    /// <summary>Every script of the commercial screens (A / B / C / Details / E / settings and D's five tabs).</summary>
    private static IEnumerable<string> CommercialScripts()
        => Directory.GetFiles(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Commercial"), "*.js", SearchOption.AllDirectories);

    private static string Code(string file)
        => Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*[\s\S]*?\*/", ""), @"(^|[^:'])//.*$", "$1", RegexOptions.Multiline);

    [Fact]
    public void P19_no_commercial_script_asks_through_the_browser_box_and_the_shared_confirmation_runs_only_on_yes()
    {
        // G-6 / G-7 — a source scan of every commercial script: window.confirm and a bare confirm( are gone
        var scripts = CommercialScripts().ToList();
        Assert.True(scripts.Count >= 25);
        foreach (var file in scripts)
        {
            var code = Code(file);
            Assert.DoesNotMatch(@"window\s*\.\s*confirm\b", code);
            Assert.DoesNotMatch(@"(?<![\w.$])confirm\s*\(", code);
        }

        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Ui() + @"
globalThis.L10n = {};
const out = { calls: [], ran: 0 };
U.confirm('T', 'S', 'B', 'danger', () => { out.ran++; });                       // no shared dialog → nothing happens (fail closed)
out.withoutDialog = out.ran;
globalThis.showConfirm = (title, cb, o) => { out.calls.push({ title: title, subtext: o.subtext, type: o.type, button: o.confirmButtonText }); out.cb = cb; };
U.confirm('Approve?', 'MG-ORD-1 goes to the depot', 'Approve', 'success', () => { out.ran++; });
out.beforeYes = out.ran;
out.cb();
out.afterYes = out.ran;
process.stdout.write(JSON.stringify(out));")!;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(0, r.GetProperty("withoutDialog").GetInt32());
        var call = r.GetProperty("calls")[0];
        Assert.Equal(("Approve?", "MG-ORD-1 goes to the depot", "success", "Approve"), (call.GetProperty("title").GetString(), call.GetProperty("subtext").GetString(), call.GetProperty("type").GetString(), call.GetProperty("button").GetString()));
        Assert.Equal(0, r.GetProperty("beforeYes").GetInt32());                                         // asking is not doing
        Assert.Equal(1, r.GetProperty("afterYes").GetInt32());

        // the acts that ask: discard (A), submit / discard (price list), discard (import), submit / discard / retire (rule), approve (C)
        Assert.Contains("U.confirm(L().DiscardDraft, L().DiscardConfirm, L().DiscardDraft, 'danger'", Script("form.js"));
        var js = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Commercial");
        Assert.Equal(2, Regex.Matches(File.ReadAllText(Path.Combine(js, "PriceLists", "details.js")), @"U\.confirm\(").Count);
        Assert.Single(Regex.Matches(File.ReadAllText(Path.Combine(js, "PriceLists", "import.js")), @"U\.confirm\("));
        Assert.Equal(3, Regex.Matches(File.ReadAllText(Path.Combine(js, "PricingRules", "details.js")), @"U\.confirm\(").Count);
    }

    [Fact]
    public void P20_approve_asks_through_the_shared_confirmation_and_nothing_is_decided_before_yes()
    {
        // G-7 — screen C driven in Node on a minimal DOM: the click opens the dialog, the decision travels only on its yes
        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Ui() + @"
const els = {};
const el = (id) => els[id] || (els[id] = { id: id, attrs: id === 'po-root' ? { 'data-screen': 'approve', 'data-order-id': 'o1' } : {}, listeners: {}, innerHTML: '', textContent: '', disabled: false, className: '',
  classList: { add() {}, remove() {}, toggle() {} }, getAttribute(n) { return this.attrs[n] || null; }, addEventListener(n, f) { this.listeners[n] = f; } });
globalThis.window = globalThis;
globalThis.document = { getElementById: el, addEventListener: (n, f) => { globalThis.ready = f; }, documentElement: { lang: 'en' } };
globalThis.L10n = { Approve: 'Approve', ApproveConfirmTitle: 'Approve the order?', ApproveConfirmText: '{0} is approved' };
const calls = [];
globalThis.fetch = (url, o) => { calls.push({ url: url, method: o.method, body: o.body || null });
  const order = { id: 'o1', code: 'MG-ORD-000009', version: 3, status: 'pending_approval', priceMode: 'quantity', lines: [], can: { decide: true }, approvalRequirement: { reasons: [] } };
  const body = url.endsWith('/pharmacy-history') ? { data: { items: [] } } : { data: order };
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify(body)) }); };
const asked = [];
globalThis.showConfirm = (title, cb, o) => { asked.push({ title: title, text: o.subtext, type: o.type }); globalThis.yes = cb; };
" + "require('" + Path.Combine(ScriptDir(), "approve.js").Replace("\\", "/") + "');" + @"
(async () => {
  globalThis.ready();
  await new Promise((r) => setTimeout(r, 20));
  els['po-approve'].listeners.click();
  await new Promise((r) => setTimeout(r, 20));
  const before = calls.filter((c) => c.url.endsWith('/decision')).length;
  globalThis.yes();
  await new Promise((r) => setTimeout(r, 20));
  const decisions = calls.filter((c) => c.url.endsWith('/decision'));
  process.stdout.write(JSON.stringify({ asked: asked, before: before, after: decisions.length, body: decisions[0] ? JSON.parse(decisions[0].body) : null }));
})();")!;
        var r = JsonDocument.Parse(node).RootElement;
        var asked = Assert.Single(r.GetProperty("asked").EnumerateArray());
        Assert.Equal("Approve the order?", asked.GetProperty("title").GetString());
        Assert.Equal("MG-ORD-000009 is approved", asked.GetProperty("text").GetString());
        Assert.Equal("success", asked.GetProperty("type").GetString());
        Assert.Equal(0, r.GetProperty("before").GetInt32());                                            // the click alone decided nothing
        Assert.Equal(1, r.GetProperty("after").GetInt32());
        Assert.Equal("approve", r.GetProperty("body").GetProperty("decision").GetString());
        Assert.Equal(3, r.GetProperty("body").GetProperty("expectedVersion").GetInt32());
    }

    [Fact]
    public void P21_after_send_the_order_opens_on_its_details_page_and_the_toast_travels_there()
    {
        // G-8 — the send handler leaves the form for Details; the Details page shows the carried toast once
        var form = Script("form.js");
        var send = Regex.Match(form, @"const send = async \(\) => \{[\s\S]*?\n    \};").Value;
        Assert.Contains("U.flashToast(order.status === 'pending_approval' ? L().SubmittedPending : L().SubmittedApproved, 'success');", send);
        Assert.Contains("window.location.href = '/Commercial/PreOrders/Details/' + encodeURIComponent(order.id);", send);
        Assert.DoesNotContain("fill(order)", send);                                                     // no longer drawn locked on the form
        Assert.Contains("U.takeFlashToast();", Script("details.js"));

        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Ui() + @"
const store = {};
globalThis.sessionStorage = { setItem: (k, v) => { store[k] = v; }, getItem: (k) => (k in store ? store[k] : null), removeItem: (k) => { delete store[k]; } };
const shown = [];
globalThis.showToast = (t, k) => shown.push([t, k]);
U.flashToast('Sent: waiting for approval', 'success');
U.takeFlashToast();
U.takeFlashToast();                                                                                     // once
process.stdout.write(JSON.stringify(shown));")!;
        var shown = JsonDocument.Parse(node).RootElement;
        Assert.Equal(1, shown.GetArrayLength());
        Assert.Equal("Sent: waiting for approval", shown[0][0].GetString());
    }

    [Fact]
    public void P22_the_summary_rows_are_the_crm_fields_in_the_mockup_order_and_no_amount_is_computed_on_the_page()
    {
        // V-3 — a source scan first: no arithmetic on an amount field anywhere in the pre-order scripts
        const string fields = "listSubtotal|discountAmount|net|vat|gross|netAmount|vatAmount|unitPrice|effectiveDiscountPct|combinedDiscountPct|localGross";
        foreach (var file in Directory.GetFiles(ScriptDir(), "*.js"))
        {
            var code = Code(file);
            Assert.DoesNotMatch(@"\.(?:" + fields + @")\b\s*[-*/]", code);
            Assert.DoesNotMatch(@"[-*/]\s*(?:Number\()?[\w.\[\]]*\.(?:" + fields + @")\b", code);
            Assert.DoesNotMatch(@"\.(?:listSubtotal|discountAmount|net|vat)\b\s*\+\s*(?:Number\()?[\w.\[\]]*\.(?:" + fields + @")\b", code);
        }

        if (RunNode("process.stdout.write('ok')") is null) return;
        var words = new Dictionary<string, string>
        {
            ["TotalListSubtotal"] = "Ara toplam (KDV hariç, liste)", ["TotalDiscount"] = "İskonto", ["TotalNet"] = "Net (KDV hariç)", ["TotalVat"] = "KDV",
            ["TotalVatRate"] = "KDV {0}", ["TotalEffective"] = "Etkin indirim (MF + iskonto)", ["TotalGross"] = "Genel toplam"
        };
        var node = RunNode(Ui() + "globalThis.L10n = " + JsonSerializer.Serialize(words) + "; globalThis.document = { documentElement: { lang: 'tr' }, getElementById: () => null };" + @"
const totals = { listSubtotal: 1100, discountAmount: 110.5, net: 989.5, vat: 98.95, gross: 1088.45, effectiveDiscountPct: 24.3 };
const one = U.totals({ priceMode: 'priced', currency: 'TRY', totals: totals, lines: [{ vatRate: 10 }, { vatRate: 10 }] });
const mixed = U.totals({ priceMode: 'priced', currency: 'TRY', totals: totals, lines: [{ vatRate: 10 }, { vatRate: 1 }] });
const old = U.totals({ priceMode: 'priced', currency: 'TRY', totals: { net: 989.5, vat: 98.95, gross: 1088.45, effectiveDiscountPct: 24.3 }, lines: [] });
const depot = U.totals({ priceMode: 'priced', currency: 'TRY', totals: totals, lines: [] }, { hideEffective: true });
const rows = (h) => (h.match(/po-total-[A-Za-z]+/g) || []);
process.stdout.write(JSON.stringify({ rows: rows(one), one: one, mixed: mixed, oldRows: rows(old), depotRows: rows(depot) }));")!;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(["po-total-listSubtotal", "po-total-discountAmount", "po-total-net", "po-total-vat", "po-total-effectiveDiscountPct", "po-total-gross"],
            r.GetProperty("rows").EnumerateArray().Select(x => x.GetString()!).ToArray());              // the mockup's order
        var one = r.GetProperty("one").GetString()!;
        foreach (var text in new[] { "Ara toplam (KDV hariç, liste)", "1.100,00 TRY", "İskonto", "− 110,50 TRY", "Net (KDV hariç)", "989,50 TRY", "KDV %10", "98,95 TRY", "Etkin indirim (MF + iskonto)", "%24,3", "Genel toplam", "1.088,45 TRY" })
        {
            Assert.Contains(text, one);                                                                 // each value is the field as it came
        }

        Assert.DoesNotContain("KDV %", r.GetProperty("mixed").GetString()!);                             // two VAT rates: no single rate is claimed
        Assert.Equal(["po-total-net", "po-total-vat", "po-total-effectiveDiscountPct", "po-total-gross"], r.GetProperty("oldRows").EnumerateArray().Select(x => x.GetString()!).ToArray()); // nothing derived
        Assert.DoesNotContain("po-total-effectiveDiscountPct", r.GetProperty("depotRows").EnumerateArray().Select(x => x.GetString()!));
    }

    [Fact]
    public void P23_the_lines_are_one_table_from_a_wider_column_with_the_price_source_and_the_header_and_button_follow_the_order()
    {
        // V-1 — the head once, a grid row per line; a card below 36rem of COLUMN width (container query, not the viewport)
        var css = File.ReadAllText(Path.Combine(ScriptDir(), "preorders.css")).Replace("\r\n", "\n");
        Assert.Contains(".po-lines-table {\n    container-type: inline-size;", css);
        Assert.Contains("@container (min-width: 36rem)", css);
        Assert.Contains("@container (min-width: 44rem)", css);
        Assert.Contains("id=\"po-lines\" class=\"po-lines-table\"", View("PreOrders/_Form.cshtml"));
        Assert.Contains("U.lineHeadHtml(priced) + S.lines.map((l) => U.formLineHtml(l, priced, isLocked)).join('')", Script("form.js"));
        // V-5 — the status badge sits in the Golden header's h5, the send button's words are its own span
        var view = View("PreOrders/_Form.cshtml");
        Assert.Contains("<h5 class=\"mb-0 d-flex flex-wrap align-items-center gap-2\"><span id=\"po-title\">", view);
        Assert.Contains("<span id=\"po-status\" class=\"d-none\"></span></h5>", view);
        Assert.Contains("<span id=\"po-send-text\">@Localizer[\"Send\"]</span>", view);

        if (RunNode("process.stdout.write('ok')") is null) return;
        var words = new Dictionary<string, string>
        {
            ["ColProduct"] = "Ürün", ["ColQty"] = "Adet", ["ColMf"] = "MF", ["ColDiscount"] = "İsk. %", ["ColUnitPrice"] = "Birim fiyat", ["ColLineTotal"] = "Satır toplamı",
            ["PriceSource"] = "Fiyat: {0} · {1} · {2} tarihinden geçerli", ["Tier_pharmacist_purchase"] = "Eczacı alış", ["TotalVatRate"] = "KDV {0}", ["GtinShort"] = "GTIN {0}", ["PerBox"] = "{0} / kutu"
        };
        var node = RunNode(Ui() + "globalThis.L10n = " + JsonSerializer.Serialize(words) + "; globalThis.document = { documentElement: { lang: 'tr' }, getElementById: () => null };" + @"
const heads = (h) => (h.match(/<span class=""po-head-[a-z]+"">[^<]*<\/span>/g) || []).map((s) => s.replace(/<[^>]+>/g, ''));
const priced = U.formLineHtml({ key: 'l1', skuId: 'a', skuText: 'KONSET' }, true, false);
const qty = U.formLineHtml({ key: 'l1', skuId: 'a', skuText: 'KONSET' }, false, false);
const cells = U.lineCells({ unitPrice: 110, netAmount: 1100 }, { priced: true, currency: 'TRY' });
process.stdout.write(JSON.stringify({
  headPriced: heads(U.lineHeadHtml(true)), headQty: heads(U.lineHeadHtml(false)),
  pricedCells: [priced.includes('id=""po-unit-l1""'), priced.includes('id=""po-total-l1""')], qtyCells: [qty.includes('po-unit-'), qty.includes('po-total-')],
  unit: cells.unit, total: cells.total, qtyResult: U.lineCells({ unitPrice: 110, netAmount: 1100 }, { priced: false }),
  source: U.priceSourceHtml({ name: 'KONSET TR', validFrom: '2026-10-01' }, 'pharmacist-purchase', 10),
  facts: U.skuFactsText({ form: 'Tablet', packQuantity: 28, packUomCode: 'C62', gtin: '8699514010015' }), noFacts: U.skuFactsText({ code: 'X' }),
  send: [C.sendLabelKey({ approvalRequired: true }, null), C.sendLabelKey({ approvalRequired: false }, null), C.sendLabelKey(null, { status: 'draft', approvalRequirement: { required: true } }), C.sendLabelKey(null, null)],
  products: C.productSummary({ lines: [{ sku: { name: 'ALMIBA' }, quantity: 50 }, { sku: { code: 'URO' }, quantity: 20 }] })
}));")!;
        var r = JsonDocument.Parse(node).RootElement;
        static string[] Arr(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(["Ürün", "Adet", "MF", "İsk. %", "Birim fiyat", "Satır toplamı"], Arr(r.GetProperty("headPriced")));
        Assert.Equal(["Ürün", "Adet", "MF"], Arr(r.GetProperty("headQty")));                            // AC-PRICE-3: no price column in quantity mode
        Assert.All(r.GetProperty("pricedCells").EnumerateArray(), x => Assert.True(x.GetBoolean()));
        Assert.All(r.GetProperty("qtyCells").EnumerateArray(), x => Assert.False(x.GetBoolean()));
        Assert.Contains("110,00 TRY", r.GetProperty("unit").GetString());
        Assert.Contains("1.100,00 TRY", r.GetProperty("total").GetString());
        Assert.Equal("—", r.GetProperty("qtyResult").GetProperty("total").GetString());
        var source = r.GetProperty("source").GetString()!;                                              // V-2: list · tier · valid from · VAT
        foreach (var text in new[] { "KONSET TR", "Eczacı alış", "2026", "KDV %10" }) Assert.Contains(text, source);
        Assert.Equal("Tablet · 28 C62 · GTIN 8699514010015", r.GetProperty("facts").GetString());
        Assert.Equal("", r.GetProperty("noFacts").GetString());
        Assert.Equal(["SendForApproval", "Send", "SendForApproval", "Send"], Arr(r.GetProperty("send")));
        Assert.Equal("ALMIBA", r.GetProperty("products")[0].GetProperty("name").GetString());          // V-4
        Assert.Equal("URO", r.GetProperty("products")[1].GetProperty("name").GetString());
        Assert.Equal(20, r.GetProperty("products")[1].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public void P25_screen_c_draws_the_crm_s_six_months_the_effective_trend_and_month_orders_amount_effective_with_no_arithmetic_of_its_own()
    {
        // V-9 — the request's answer goes to the drawing as it is; the core no longer buckets, the drawing has no sum / net / weight
        var approve = Script("approve.js");
        Assert.Contains("U.historyHtml(d.months || [])", approve);
        Assert.DoesNotContain("historyMonths", Script("preorders-core.js") + approve);
        var ui = Script("preorders-ui.js");
        var drawing = ui[ui.IndexOf("const historyHtml = ", StringComparison.Ordinal)..ui.IndexOf("const toast = ", StringComparison.Ordinal)];
        Assert.DoesNotMatch(@"\+=|\.reduce\(|\.net\b|\.quantity\b|\.items\b|createdAt|priceMode|status", drawing);
        Assert.Contains("m.effectiveDiscountPct", drawing);
        Assert.Contains("(m.gross || []).map((g) => money(g.amount, g.currency))", drawing);          // each currency on its own
        Assert.DoesNotContain("ColBoxes", ui);

        if (RunNode("process.stdout.write('ok')") is null) return;
        var words = Page("tr");
        var node = RunNode(Ui() + "globalThis.L10n = " + JsonSerializer.Serialize(words) + "; globalThis.document = { documentElement: { lang: 'tr' }, getElementById: () => null };" + @"
const html = U.historyHtml([
  { month: '2026-09', orders: 0, boxes: 0, gross: [], effectiveDiscountPct: null },
  { month: '2026-10', orders: 4, boxes: 24, gross: [{ currency: 'EUR', amount: 236 }, { currency: 'TRY', amount: 1180 }], effectiveDiscountPct: 18 }]);
process.stdout.write(JSON.stringify({ html: html, empty: U.historyHtml([]) }));")!;
        var r = JsonDocument.Parse(node).RootElement;
        var html = r.GetProperty("html").GetString()!;
        Assert.Contains(words["HistoryTrend"], html);                                                   // the chart is the effective discount trend
        var heads = Regex.Matches(html, "<th[^>]*>([^<]*)</th>").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal([words["ColMonth"], words["ColOrders"], words["ColAmount"], words["ColEffectiveShort"]], heads); // mockup C
        Assert.Contains("%18", html);                                                                   // the CRM's percentage, Turkish
        Assert.Contains("236,00 EUR · 1.180,00 TRY", html);                                            // never EUR + TRY
        Assert.Contains("--po-bar:100%", html);                                                         // the tallest month
        Assert.Contains("po-bar po-bar-none\" style=\"--po-bar:0%", html);                             // a month without priced orders: no bar …
        Assert.Equal(3, Regex.Matches(html, ">—<").Count);                                              // … and "—" over the bar, for its amount and its %
        Assert.Contains("<tbody></tbody>", r.GetProperty("empty").GetString()!);
        Assert.Equal(("Tutar", "Etkin", "Etkin indirim eğilimi (MF + iskonto)"), (words["ColAmount"], words["ColEffectiveShort"], words["HistoryTrend"]));
    }

    [Fact]
    public void P26_the_reports_to_seat_is_written_by_its_name_and_by_its_code_only_without_one()
    {
        // V-10 — approvalRequirement.approverPositionName (CRM, written at submission); an older order has only the code
        var approve = Script("approve.js");
        var line = Regex.Match(approve, @"byId\('po-you'\)\.innerHTML = [^\n]+").Value;
        Assert.False(string.IsNullOrEmpty(line), "the reports-to line moved");
        if (RunNode("process.stdout.write('ok')") is null) return;
        var words = Page("tr");
        // the production line itself, run with the order's requirement (an order submitted after / before W6-FIX-d)
        var node = RunNode(Ui() + "globalThis.L10n = " + JsonSerializer.Serialize(words) + "; globalThis.document = { documentElement: { lang: 'tr' }, getElementById: () => null };"
            + "const you = (req) => { const el = {}; const byId = () => el; const o = { rep: { displayName: 'Sema' } }; " + line + " return el.innerHTML; };"
            + "process.stdout.write(JSON.stringify([you({ approverPositionCode: 'POS-MED-DIR', approverPositionName: 'Bölge Müdürü' }), you({ approverPositionCode: 'POS-MED-DIR', approverPositionName: null })]));")!;
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Contains("Bölge Müdürü", r[0]);
        Assert.DoesNotContain("POS-MED-DIR", r[0]);
        Assert.Contains("POS-MED-DIR", r[1]);
    }

    [Fact]
    public void P24_at_1280_px_the_rep_lines_effective_and_rule_columns_outrank_the_depot_and_the_date_and_the_local_column_is_in_column_visibility()
    {
        var index = Script("index.js");
        var map = Regex.Match(index, @"const PRIORITY = \{([^}]*)\};").Groups[1].Value;
        var priority = Regex.Matches(map, @"(\d+): (\d+)").ToDictionary(m => int.Parse(m.Groups[1].Value), m => int.Parse(m.Groups[2].Value));
        Assert.Equal(10, priority.Count);                                                               // every data column but the code (always shown)
        foreach (var column in new[] { 5, 6, 9, 11 })                                                    // rep · lines · effective · rule (V-7)
        {
            Assert.True(priority[column] < priority[4] && priority[column] < priority[2], "column " + column);
            Assert.Contains("{ targets: " + column + ", responsivePriority: PRIORITY[" + column + "]", index);
        }

        Assert.Equal(priority.Values.Max(), priority[8]);                                               // the local equivalent goes first
        Assert.Contains("max-inline-size: 13rem;", File.ReadAllText(Path.Combine(ScriptDir(), "preorders.css")));
        // V-8 — measured: the local equivalent IS a Column Visibility entry (off by default)
        Assert.Contains("const DATA_COLUMNS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];", index);
        Assert.Contains("colvisColumns: DATA_COLUMNS,", index);
        Assert.Contains("DATA_COLUMNS.filter((c) => c !== 8", index);
        Assert.Contains("Header = Localizer[\"ColLocal\"].Value", View("PreOrders/_DataTable.cshtml"));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static (string, string) Take((string Method, string Path, string Body) call) => (call.Method, call.Path);

    private static JsonElement Json(object? value) => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        FileContentResult => 200,
        _ => null
    };

    private static (FakeGateway Gateway, PreOrdersController Controller) Arrange(string[] permissions, string? answer = null, string? body = null, string? acceptLanguage = null)
    {
        var gateway = new FakeGateway(answer ?? "{\"data\":{}}");
        var controller = new PreOrdersController(new HttpClient(gateway), Config(), NullLogger<PreOrdersController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = Http(permissions, body, acceptLanguage) };
        return (gateway, controller);
    }

    private static (FakeGateway Gateway, PreOrderSettingsController Controller) ArrangeSettings(string[] permissions, string? answer = null)
    {
        var gateway = new FakeGateway(answer ?? "{\"data\":{}}");
        var controller = new PreOrderSettingsController(new HttpClient(gateway), Config(), NullLogger<PreOrderSettingsController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = Http(permissions, "{\"approvalMode\":\"always\"}", null) };
        return (gateway, controller);
    }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();

    private static DefaultHttpContext Http(string[] permissions, string? body, string? acceptLanguage)
    {
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>(new NoTempData());
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        services.AddSingleton<TimeProvider>(new PinnedClock(Today));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (body is not null)
        {
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            http.Request.ContentType = "application/json";
        }

        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        return http;
    }

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

    /// <summary>The Gateway: one answer for every call ("%PDF" = a PDF with a file name); every call recorded.</summary>
    private sealed class FakeGateway(string answer) : HttpMessageHandler
    {
        public List<(string Method, string Path, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            lock (Calls)
            {
                Calls.Add((request.Method.Method, uri[GatewayUrl.Length..], body));
            }

            if (answer == "%PDF")
            {
                var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 test"));
                pdf.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                pdf.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "MG-ORD-000001-pharmacy.pdf" };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = pdf };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private static string Ui()
        => "const C = require('" + Path.Combine(ScriptDir(), "preorders-core.js").Replace("\\", "/") + "');\n"
           + "require('" + Common("commercial-rules.js", path: true) + "');\n"
           + "const U = require('" + Path.Combine(ScriptDir(), "preorders-ui.js").Replace("\\", "/") + "');\n";

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the C# assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "po-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Commercial", "PreOrders");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string Common(string file, bool path = false)
    {
        var full = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Commercial", file);
        return path ? full.Replace("\\", "/") : File.ReadAllText(full).Replace("\r\n", "\n");
    }

    private static string View(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "Commercial", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string relative)
        => XDocument.Load(Path.Combine(WebRoot(), "Resources", relative)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, string> Page(string culture)
        => Resx(Path.Combine("Views", "Commercial", "PreOrders", "PreOrdersIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx"));

    private static Dictionary<string, string> Rules(string culture)
        => Resx(Path.Combine("Views", "Commercial", "CommercialRules" + (culture.Length == 0 ? "" : "." + culture) + ".resx"));

    private static Dictionary<string, string> Help(string culture) => Resx("PageHelpResource." + culture + ".resx");

    private static string Repo() => Path.GetDirectoryName(Path.GetDirectoryName(WebRoot()))!;

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
