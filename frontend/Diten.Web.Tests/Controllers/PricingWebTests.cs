using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.Commercial;
using Diten.Web.Models.Commercial;
using Diten.Web.Views.Commercial.Pricing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W6 · W6-WEB-b — screen D of MOD-0156 (price lists · MF / discount rules · rule trial · change history · FX rates)
/// on the REAL views, proxy, resx and scripts: (1) every tab and page draws only the access notice without its key and a
/// tab exists only for a caller who may open it (UAS-001); (2) every proxy call checks its MOD-0156 §14 key HERE before the
/// CRM is asked and forwards to the right CRM path; (3) FOUR EYES — the proxy marks what the caller prepared and the page
/// never offers that record's decision (D13); (4) the import preview's counts each on their own; (5) no price / discount
/// formula on the page (a source scan); (6) the rule messages are W6-WEB-a's ONE shared source (no copy); (7) every word,
/// menu label and help text in seven languages, the manifest page, the gateway routes.
/// </summary>
public sealed class PricingWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c5e1b2-0000-4000-8000-000000000001");
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private const string Me = "user-1";
    private const string IdText = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid Id = Guid.Parse(IdText);

    private const string ListsRead = CommercialPricingController.ListsRead;
    private const string RulesRead = CommercialPricingController.RulesRead;
    private const string FxRead = CommercialPricingController.FxRead;

    // ═══ 1 · the pages and the tabs ═════════════════════════════════════════════════════════════════════════════

    public static TheoryData<string> Pages() =>
    [
        "PriceLists/Index.cshtml", "PriceLists/Create.cshtml", "PriceLists/Edit.cshtml", "PriceLists/Details.cshtml", "PriceLists/Import.cshtml",
        "PricingRules/Index.cshtml", "PricingRules/Create.cshtml", "PricingRules/Edit.cshtml", "PricingRules/Details.cshtml", "Pricing/Trial.cshtml",
        "PricingHistory/Index.cshtml", "FxRates/Index.cshtml", "FxRates/Create.cshtml", "FxRates/Edit.cshtml"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public void D1_every_page_draws_only_the_access_notice_without_its_key_and_sits_in_the_tenant_shell(string file)
    {
        var view = View(file);
        Assert.Contains("Layout = \"_LayoutTenantShell\";", view);
        var at = view.IndexOf("@if (!Model.Allowed)", StringComparison.Ordinal);
        Assert.True(at > 0, file + " has no guard");
        Assert.DoesNotMatch(@"<(div|section|partial|form|h5|h6|table)\b", Regex.Replace(view[..at], @"@\*[\s\S]*?\*@", ""));
        Assert.Matches(@"@if \(!Model\.Allowed\)\s*\{\s*<partial name=""_AccessDenied"" model=""@Localizer\[""[A-Za-z]+""\]\.Value"" />\s*return;\s*\}", view);
        var markup = Regex.Replace(view, @"@\*[\s\S]*?\*@", "");
        Assert.DoesNotContain("_CreateEditOffcanvas", markup);
        Assert.DoesNotContain("<h4", markup);
        Assert.DoesNotContain("breadcrumb", markup);
        Assert.DoesNotContain("style=", markup);
        Assert.Contains("<partial name=\"~/Views/Commercial/Pricing/_PricingHeader.cshtml\" model=\"Model\" />", view);   // the five-tab card on every page
    }

    [Fact]
    public void D2_each_page_is_allowed_by_its_own_key_only()
    {
        bool Allowed(string tab, string screen, params string[] keys) => CommercialPricingController.Flags(k => keys.Contains(k), tab, screen).Allowed;
        var cases = new (string Tab, string Screen, string Key)[]
        {
            ("lists", "index", ListsRead), ("lists", "details", ListsRead), ("lists", "create", "commercial.price-lists.create"), ("lists", "edit", "commercial.price-lists.update"),
            ("lists", "import", "commercial.price-lists.import"), ("rules", "index", RulesRead), ("rules", "details", RulesRead), ("rules", "create", "commercial.pricing-rules.create"),
            ("rules", "edit", "commercial.pricing-rules.update"), ("trial", "index", RulesRead), ("fx", "index", FxRead), ("fx", "create", "commercial.fx-rates.create"),
            ("fx", "edit", "commercial.fx-rates.update")
        };
        foreach (var (tab, screen, key) in cases)
        {
            Assert.True(Allowed(tab, screen, key), $"{tab}/{screen} with {key}");
            Assert.False(Allowed(tab, screen, CommercialPricingController.Keys.Where(k => k != key).ToArray()), $"{tab}/{screen} without {key}");
        }

        foreach (var key in new[] { ListsRead, RulesRead, FxRead }) Assert.True(Allowed("history", "index", key));
        Assert.False(Allowed("history", "index", "commercial.price-lists.create", "commercial.pre-orders.read"));
        Assert.False(Allowed("nope", "index", CommercialPricingController.Keys.ToArray()));

        // the controller's own pages read the same flags
        var (_, c) = Arrange([RulesRead]);
        Assert.False(Assert.IsType<PricingPageViewModel>(Assert.IsType<ViewResult>(c.Index()).Model).Allowed);
        Assert.True(Assert.IsType<PricingPageViewModel>(Assert.IsType<ViewResult>(c.Trial()).Model).Allowed);
        Assert.Equal(("rules", "details", Id), ((PricingPageViewModel)((ViewResult)c.RuleDetails(Id)).Model!).Let(m => (m.Tab, m.Screen, m.Id!.Value)));

        // a tab exists only for the caller who may open it (the header adds each under its own flag)
        var header = View("Pricing/_PricingHeader.cshtml");
        foreach (var (flag, key) in new[] { ("CanReadLists", "lists"), ("CanReadRules", "rules"), ("CanReadRules", "trial"), ("CanReadHistory", "history"), ("CanReadFx", "fx") })
        {
            Assert.Contains("Add(Model." + flag + ", \"" + key + "\"", header);
        }

        Assert.Contains("<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"card\" />", header);
        Assert.Contains("<h5 class=\"mb-0\">@Localizer[\"ScreenTitle\"]</h5>", header);
    }

    [Fact]
    public void D3_the_four_lists_are_the_shared_v2_shell_in_server_mode_without_a_selection()
    {
        foreach (var (module, table) in new[] { ("PriceLists", "dt-pricelists"), ("PricingRules", "dt-pricingrules"), ("FxRates", "dt-fxrates"), ("PricingHistory", "dt-pricinghistory") })
        {
            var dt = View(module + "/_DataTable.cshtml");
            Assert.Contains("TableId = \"" + table + "\"", dt);
            Assert.Contains("DataMode = \"server\"", dt);
            Assert.Contains("HasSelection = false", dt);
            var index = View(module + "/Index.cshtml");
            Assert.Contains("<partial name=\"~/Views/Commercial/" + module + "/_Filter.cshtml\" model=\"Model\" />", index);
            Assert.Contains("id=\"offcanvasDetailsPreview\"", index);
            var js = File.ReadAllText(Path.Combine(JsRoot(), module, "index.js"));
            Assert.Contains("window.DitenDataTable.createList({", js);
            Assert.Contains("{ targets: '_all', orderable: false }", js);                                    // the CRM orders
            Assert.Contains("<partial name=\"~/Views/Commercial/Pricing/_PricingScripts.cshtml\" />", index);
        }

        var css = Regex.Replace(Js("Pricing/pricing.css"), @"/\*[\s\S]*?\*/", "");
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", css);
        Assert.DoesNotMatch(@"rgba?\(\s*\d", css);
        Assert.Contains("var(--bs-primary)", css);
        Assert.DoesNotMatch(@"(margin|padding|border)-(left|right)\b|(^|[\s;{])(left|right)\s*:", css);
    }

    // ═══ 2 · the proxy ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D4_every_call_checks_its_key_before_the_crm_is_asked()
    {
        async Task Refused(string[] keys, Func<CommercialPricingController, Task<IActionResult>> call)
        {
            var (gw, c) = Arrange(keys);
            Assert.Equal(403, Status(await call(c)));
            Assert.Empty(gw.Calls);
        }

        var all = CommercialPricingController.Keys.ToArray();
        string[] Except(string k) => all.Where(x => x != k).ToArray();
        await Refused(Except(ListsRead), c => c.PriceListPage(0, 10, null, null, null, null, null, null, null, CancellationToken.None));
        await Refused(Except(ListsRead), c => c.List(Id, CancellationToken.None));
        await Refused(Except("commercial.price-lists.create"), c => c.CreateList(CancellationToken.None));
        await Refused(Except("commercial.price-lists.update"), c => c.UpdateList(Id, CancellationToken.None));
        await Refused(Except("commercial.price-lists.import"), c => c.Upload(CancellationToken.None));
        await Refused(Except("commercial.price-lists.import"), c => c.Import(Id, null, null, null, null, CancellationToken.None));
        await Refused(Except("commercial.price-lists.import"), c => c.ImportAction(Id, "convert", CancellationToken.None));
        await Refused(Except("commercial.price-lists.export"), c => c.ExportLists("csv", null, null, null, null, null, null, null, null, CancellationToken.None));
        await Refused(Except(RulesRead), c => c.RulePage(0, 10, null, null, null, null, null, CancellationToken.None));
        await Refused(Except(RulesRead), c => c.RuleTrial(CancellationToken.None));
        await Refused(Except("commercial.pricing-rules.create"), c => c.CreateRule(CancellationToken.None));
        await Refused(Except("commercial.pricing-rules.update"), c => c.UpdateRule(Id, CancellationToken.None));
        await Refused(Except(FxRead), c => c.FxPage(0, 10, null, null, null, null, null, null, CancellationToken.None));
        await Refused(Except(FxRead), c => c.FxRate(Id, CancellationToken.None));
        await Refused(Except("commercial.fx-rates.create"), c => c.CreateFx(CancellationToken.None));
        await Refused(Except("commercial.fx-rates.update"), c => c.UpdateFx(Id, CancellationToken.None));
        await Refused(["commercial.price-lists.create", "commercial.fx-rates.create"], c => c.HistoryPage(0, 10, null, null, null, null, null, CancellationToken.None));
        foreach (var (action, key) in CommercialPricingController.ListActionKeys) await Refused(Except(key), c => c.ListAction(Id, action, CancellationToken.None));
        foreach (var (action, key) in CommercialPricingController.RuleActionKeys) await Refused(Except(key), c => c.RuleAction(Id, action, CancellationToken.None));

        // the tables are MOD-0156 §14 / §15
        Assert.Equal(new Dictionary<string, string> { ["versions"] = "commercial.price-lists.create", ["discard"] = "commercial.price-lists.create", ["submit"] = "commercial.price-lists.submit", ["decision"] = "commercial.price-lists.publish" }
            .OrderBy(x => x.Key), CommercialPricingController.ListActionKeys.OrderBy(x => x.Key));
        Assert.Equal(new Dictionary<string, string> { ["revisions"] = "commercial.pricing-rules.update", ["discard"] = "commercial.pricing-rules.update", ["submit"] = "commercial.pricing-rules.submit", ["decision"] = "commercial.pricing-rules.publish" }
            .OrderBy(x => x.Key), CommercialPricingController.RuleActionKeys.OrderBy(x => x.Key));

        // an unknown action is 404, never forwarded
        var (g404, c404) = Arrange(all);
        Assert.IsType<NotFoundResult>(await c404.ListAction(Id, "delete", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await c404.RuleAction(Id, "activate", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await c404.ImportAction(Id, "publish", CancellationToken.None));
        Assert.Empty(g404.Calls);
    }

    [Fact]
    public async Task D5_with_its_key_each_call_goes_to_its_crm_path_and_the_list_contract_is_refused_before_any_call()
    {
        async Task<(string, string)> One(string[] keys, Func<CommercialPricingController, Task<IActionResult>> call)
        {
            var (gw, c) = Arrange(keys, body: "{\"expectedVersion\":2}");
            Assert.Equal(200, Status(await call(c)));
            return (gw.Calls[0].Method, gw.Calls[0].Path);
        }

        foreach (var (action, key) in CommercialPricingController.ListActionKeys) Assert.Equal(("POST", "/api/commercial/price-lists/" + IdText + "/" + action), await One([key], c => c.ListAction(Id, action, CancellationToken.None)));
        foreach (var (action, key) in CommercialPricingController.RuleActionKeys) Assert.Equal(("POST", "/api/commercial/pricing-rules/" + IdText + "/" + action), await One([key], c => c.RuleAction(Id, action, CancellationToken.None)));
        Assert.Equal(("POST", "/api/commercial/pricing-rules/trial"), await One([RulesRead], c => c.RuleTrial(CancellationToken.None)));
        Assert.Equal(("PUT", "/api/commercial/fx-rates/" + IdText), await One(["commercial.fx-rates.update"], c => c.UpdateFx(Id, CancellationToken.None)));
        Assert.Equal(("POST", "/api/commercial/price-list-imports/" + IdText + "/convert"), await One(["commercial.price-lists.import"], c => c.ImportAction(Id, "convert", CancellationToken.None)));
        Assert.Equal("GET", (await One([ListsRead], c => c.Items(Id, null, "previous", 1, 50, CancellationToken.None))).Item1);

        var (gw, ctl) = Arrange([ListsRead], answer: "{\"data\":{\"items\":[],\"total\":0}}");
        Assert.Equal(200, Status(await ctl.PriceListPage(20, 10, null, null, IdText, "tr", "try", "official", "in-force", CancellationToken.None)));
        var path = gw.Calls[0].Path;
        foreach (var part in new[] { "legalEntityId=" + IdText, "countryCode=TR", "currency=TRY", "kind=official", "status=in-force", "page=3", "pageSize=10" }) Assert.Contains(part, path);

        foreach (var call in new Func<CommercialPricingController, Task<IActionResult>>[]
                 {
                     x => x.PriceListPage(0, 10, null, "name", null, null, null, null, null, CancellationToken.None),
                     x => x.PriceListPage(0, 10, null, null, "company-1", null, null, null, null, CancellationToken.None),
                     x => x.PriceListPage(0, 10, null, null, null, null, null, "free", null, CancellationToken.None),
                     x => x.PriceListPage(0, 10, null, null, null, null, null, null, "published-ish", CancellationToken.None),
                     x => x.RulePage(0, 10, null, null, null, null, "live", CancellationToken.None),
                     x => x.FxPage(0, 10, null, null, null, null, "21.10.2026", null, CancellationToken.None),
                     x => x.HistoryPage(0, 10, null, "order", null, null, null, CancellationToken.None),
                     x => x.Items(Id, null, "next", null, null, CancellationToken.None)
                 })
        {
            var (g, c) = Arrange(CommercialPricingController.Keys.ToArray());
            Assert.Equal(400, Status(await call(c)));
            Assert.Empty(g.Calls);
        }
    }

    // ═══ 3 · four eyes ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D6_the_proxy_marks_what_the_caller_prepared_and_the_page_never_offers_its_decision()
    {
        JsonObject Rec(string? created, string? updated, string? submitted) => JsonNode.Parse(JsonSerializer.Serialize(new
        {
            id = IdText, status = "in_review", createdBy = created, updatedBy = updated, rounds = new[] { new { roundNo = 1, submittedBy = submitted, outcome = (string?)null } }
        }))!.AsObject();
        Assert.True(CommercialPricingController.IsMine(Rec(Me, "other", "other"), Me));
        Assert.True(CommercialPricingController.IsMine(Rec("other", " USER-1 ", "other"), Me));
        Assert.True(CommercialPricingController.IsMine(Rec("other", "other", Me), Me));
        Assert.False(CommercialPricingController.IsMine(Rec("other", "other", "other"), Me));
        Assert.False(CommercialPricingController.IsMine(Rec(Me, Me, Me), null));

        // the CRM's answer is passed on with `mine` on the record, the page's items and a rule detail
        var list = "{\"data\":{\"id\":\"" + IdText + "\",\"status\":\"in_review\",\"createdBy\":\"user-1\",\"rounds\":[]}}";
        var (_, c1) = Arrange([ListsRead], answer: list);
        Assert.True(Json(Assert.IsType<OkObjectResult>(await c1.List(Id, CancellationToken.None)).Value).GetProperty("data").GetProperty("mine").GetBoolean());
        var rule = "{\"data\":{\"rule\":{\"id\":\"" + IdText + "\",\"status\":\"in_review\",\"createdBy\":\"someone\",\"rounds\":[{\"submittedBy\":\"user-1\",\"outcome\":null}]},\"versions\":[{\"id\":\"x\",\"status\":\"active\",\"createdBy\":\"someone\"}]}}";
        var (_, c2) = Arrange([RulesRead], answer: rule);
        var d = Json(Assert.IsType<OkObjectResult>(await c2.Rule(Id, CancellationToken.None)).Value).GetProperty("data");
        Assert.True(d.GetProperty("rule").GetProperty("mine").GetBoolean());
        Assert.False(d.GetProperty("versions")[0].GetProperty("mine").GetBoolean());
        var page = "{\"data\":{\"items\":[{\"id\":\"a\",\"status\":\"draft\",\"createdBy\":\"user-1\"},{\"id\":\"b\",\"status\":\"in_review\",\"createdBy\":\"x\"}],\"total\":2}}";
        var (_, c3) = Arrange([ListsRead], answer: page);
        var items = Json(Assert.IsType<OkObjectResult>(await c3.PriceListPage(0, 10, null, null, null, null, null, null, null, CancellationToken.None)).Value).GetProperty("data").GetProperty("items");
        Assert.Equal(new[] { true, false }, items.EnumerateArray().Select(i => i.GetProperty("mine").GetBoolean()));

        // the page: the decision only for a record in review, with the publish key, NOT prepared by the caller
        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Core() + @"
const f = { canPublish: true, canUpdate: true, canCreate: true, canSubmit: true };
const out = {
  listOther: K.listActions({ status: 'in_review', mine: false }, f),
  listMine: K.listActions({ status: 'in_review', mine: true }, f),
  listNoKey: K.listActions({ status: 'in_review', mine: false }, { canPublish: false }),
  listDraft: K.listActions({ status: 'draft', mine: true }, f),
  listPublished: K.listActions({ status: 'published' }, f),
  ruleOther: K.ruleActions({ status: 'in_review', mine: false }, f),
  ruleMine: K.ruleActions({ status: 'in_review', mine: true }, f),
  ruleActive: K.ruleActions({ status: 'active' }, f),
  htmlOther: PU.decisionHtml(K.listActions({ status: 'in_review', mine: false }, f)),
  htmlMine: PU.decisionHtml(K.listActions({ status: 'in_review', mine: true }, f))
};
process.stdout.write(JSON.stringify(out));")!;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.True(r.GetProperty("listOther").GetProperty("decide").GetBoolean());
        Assert.False(r.GetProperty("listMine").GetProperty("decide").GetBoolean());
        Assert.True(r.GetProperty("listMine").GetProperty("ownDecision").GetBoolean());
        Assert.False(r.GetProperty("listNoKey").GetProperty("decide").GetBoolean());
        Assert.True(r.GetProperty("listDraft").GetProperty("edit").GetBoolean());
        Assert.False(r.GetProperty("listDraft").GetProperty("decide").GetBoolean());
        Assert.False(r.GetProperty("listPublished").GetProperty("edit").GetBoolean());                    // a published list is not edited …
        Assert.True(r.GetProperty("listPublished").GetProperty("newVersion").GetBoolean());               // … it gets a new version
        Assert.True(r.GetProperty("ruleOther").GetProperty("decide").GetBoolean());
        Assert.False(r.GetProperty("ruleMine").GetProperty("decide").GetBoolean());
        Assert.True(r.GetProperty("ruleActive").GetProperty("revise").GetBoolean());
        Assert.Contains("id=\"pr-publish\"", r.GetProperty("htmlOther").GetString());
        Assert.DoesNotContain("pr-publish", r.GetProperty("htmlMine").GetString());
        Assert.DoesNotContain("pr-return", r.GetProperty("htmlMine").GetString());
        Assert.Contains("id=\"pr-own\"", r.GetProperty("htmlMine").GetString());

        // both detail pages draw the decision area from those actions; the return needs a reason (the shared dialog)
        Assert.Contains("PU.decisionHtml(a)", Js("PriceLists/details.js"));
        Assert.Contains("PU.decisionHtml(a)", Js("PricingRules/details.js"));
        Assert.Contains("reason = await U().askReason(L().ReturnTitle, L().ReturnText, L().ReturnDraft);", Js("Pricing/pricing-ui.js"));
        Assert.Contains("if (!reason) return null;", Js("Pricing/pricing-ui.js"));
    }

    // ═══ 4 · the import ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void D7_the_import_preview_counts_each_kind_on_its_own_and_converts_only_matched_rows()
    {
        var view = View("PriceLists/Import.cshtml");
        foreach (var id in new[] { "pr-step-1", "pr-step-2", "pr-step-3", "pr-counts", "pr-diff-counts", "pr-convert", "pr-col-gtin" }) Assert.Contains("id=\"" + id + "\"", view);
        Assert.Contains("id=\"pr-col-gtin\" disabled", view);                                               // GTIN matching is off …
        Assert.Contains("@Localizer[\"GtinOffNote\"]", view);                                                // … and said in words
        Assert.Contains("id=\"pr-currency\" maxlength=\"3\" required", view);                               // AC-LIST-4: the currency is required
        if (RunNode("process.stdout.write('ok')") is null) return;
        var node = RunNode(Core() + @"
const words = { Imp_matched: 'm{0}', Imp_unmatched: 'u{0}', Imp_retired: 'r{0}', Imp_duplicate: 'd{0}', Imp_invalidPrice: 'i{0}' };
globalThis.L10n = words;
const imp = { status: 'validated', totalRows: 10, matched: 5, unmatched: 2, retired: 1, duplicate: 1, invalidPrice: 1, comparedPriceListId: 'p', added: 2, removed: 1, changed: 3, vatChanged: 0 };
const s = K.importSummary(imp);
const none = K.importSummary({ status: 'validated', matched: 0, unmatched: 4 });
const done = K.importSummary({ status: 'converted', convertedPriceListId: 'L1', matched: 3 });
process.stdout.write(JSON.stringify({ s: s, none: none, done: done }));")!;
        var r = JsonDocument.Parse(node).RootElement;
        var counts = r.GetProperty("s").GetProperty("counts").EnumerateArray().ToDictionary(c => c.GetProperty("key").GetString()!, c => c.GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["Imp_matched"] = 5, ["Imp_unmatched"] = 2, ["Imp_retired"] = 1, ["Imp_duplicate"] = 1, ["Imp_invalidPrice"] = 1 }, counts);
        var diff = r.GetProperty("s").GetProperty("diff").EnumerateArray().ToDictionary(c => c.GetProperty("key").GetString()!, c => c.GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["Diff_added"] = 2, ["Diff_removed"] = 1, ["Diff_changed"] = 3, ["Diff_vatChanged"] = 0 }, diff);
        Assert.True(r.GetProperty("s").GetProperty("canConvert").GetBoolean());
        Assert.True(r.GetProperty("s").GetProperty("compared").GetBoolean());
        Assert.False(r.GetProperty("none").GetProperty("canConvert").GetBoolean());                        // nothing matched → no draft
        Assert.False(r.GetProperty("none").GetProperty("compared").GetBoolean());
        Assert.Equal("L1", r.GetProperty("done").GetProperty("converted").GetString());
        Assert.False(r.GetProperty("done").GetProperty("canConvert").GetBoolean());                        // once
    }

    // ═══ 5 · no formula on the page ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void D8_no_price_mf_or_discount_formula_lives_in_a_screen_d_script()
    {
        var files = new[] { "Pricing", "PriceLists", "PricingRules", "FxRates", "PricingHistory" }.SelectMany(d => Directory.GetFiles(Path.Combine(JsRoot(), d), "*.js")).ToList();
        Assert.True(files.Count >= 14);
        foreach (var file in files)
        {
            var code = Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*[\s\S]*?\*/", ""), @"(^|[^:])//.*$", "$1", RegexOptions.Multiline);
            foreach (var formula in new[] { @"\bfree\s*/\s*\(", @"/\s*\(\s*\w+(\.\w+)?\s*\+\s*\w+", @"1\s*-\s*\(\s*1\s*-", @"Math\.floor\s*\(", @"\*\s*\(\s*1\s*-", @"\.buy\s*\+\s*\w+\.free\s*\)" })
            {
                Assert.DoesNotMatch(formula, code);
            }
        }

        // the MF ratio beside "10+2" is the CRM's answer: the rule form asks the trial endpoint and shows the line's mfRatioPct
        var form = Js("PricingRules/form.js");
        Assert.Contains("endpoint + '/trial'", form);
        Assert.Contains("line.mfRatioPct", form);
        Assert.Contains("t.ratioPct", form);                                                              // or the saved rule's own DTO
        Assert.Contains("id=\"pr-segment\" disabled", View("PricingRules/_Form.cshtml"));                  // the segment is "coming soon"
        Assert.Contains("pharmacySegmentCode: null", Js("Pricing/pricing-core.js"));
    }

    // ═══ 6 · the ONE rule message source ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void D9_screen_d_words_its_rules_from_the_shared_source_and_holds_no_copy()
    {
        // no Rule.* word in the screen's own resource, no second rules resource, no rule-code table in its scripts
        Assert.DoesNotContain(Page("en").Keys, k => k.StartsWith("Rule.", StringComparison.Ordinal));
        var commercial = Path.Combine(WebRoot(), "Resources", "Views", "Commercial");
        Assert.Equal(["CommercialRules"], Directory.GetFiles(commercial, "*.resx", SearchOption.AllDirectories)
            .Where(f => XDocument.Load(f).Root!.Elements("data").Any(d => ((string)d.Attribute("name")!).StartsWith("Rule.", StringComparison.Ordinal)))
            .Select(f => Path.GetFileName(f).Split('.')[0]).Distinct());
        foreach (var file in new[] { "Pricing", "PriceLists", "PricingRules", "FxRates", "PricingHistory" }.SelectMany(d => Directory.GetFiles(Path.Combine(JsRoot(), d), "*.js")))
        {
            Assert.DoesNotMatch(@"'(discount|mf|combined|quantity)\.(soft|hard)-exceeded'\s*:", File.ReadAllText(file));
            Assert.DoesNotMatch(@"'Rule\.", File.ReadAllText(file));
        }

        var scripts = View("Pricing/_PricingScripts.cshtml");
        Assert.Contains("<partial name=\"~/Views/Commercial/_CommercialRulesL10n.cshtml\" />", scripts);
        Assert.Contains("R().html(f,", Js("Pricing/trial.js"));
        // the shared source carries the MOD-0156 refusal codes too (the W6-WEB-a test reads the CRM constants)
        var rules = Resx(Path.Combine("Views", "Commercial", "CommercialRules.en.resx"));
        Assert.Contains("Rule.price_list_sod", rules.Keys);
        Assert.Contains("Rule.pricing_rule_sod", rules.Keys);
    }

    // ═══ 7 · words, help, menu, routes ═════════════════════════════════════════════════════════════════════════

    private static readonly HashSet<string> SameEverywhere = ["ValidityRange", "DecisionLine", "MfRatioIs", "RuleRef"];

    [Fact]
    public void D10_every_word_is_in_seven_languages_and_every_word_a_script_names_is_declared()
    {
        var keys = PricingIndex.ScriptKeys.Concat(PricingIndex.ViewKeys).ToList();
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
            }

            if (language != "en")
            {
                var same = keys.Count(k => english[k] == words[k] && !SameEverywhere.Contains(k));
                Assert.True(same <= 12, $"{language}: {same} words are the English ones");
            }
        }

        var scripts = string.Concat(new[] { "Pricing", "PriceLists", "PricingRules", "FxRates", "PricingHistory" }.SelectMany(d => Directory.GetFiles(Path.Combine(JsRoot(), d), "*.js")).Select(File.ReadAllText));
        var named = Regex.Matches(scripts, @"L\(\)\.([A-Z][A-Za-z0-9_]+)|L\(\)\['([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'\]|\bTh?\('([A-Z][A-Za-z0-9_]*[A-Za-z0-9])'|error: '([A-Z][A-Za-z]+)'")
            .Select(m => m.Groups.Values.Skip(1).First(g => g.Success).Value).Distinct().ToList();
        Assert.All(named.Where(n => n is not ("Actions" or "DateFormat")), n => Assert.Contains(n, PricingIndex.ScriptKeys));
        var views = string.Concat(new[] { "Pricing", "PriceLists", "PricingRules", "FxRates", "PricingHistory" }.SelectMany(d => Directory.GetFiles(Path.Combine(WebRoot(), "Views", "Commercial", d), "*.cshtml")).Select(File.ReadAllText));
        Assert.All(Regex.Matches(views, @"(?<!Shared)Localizer\[""([A-Za-z0-9_]+)""\]").Select(m => m.Groups[1].Value), k => Assert.Contains(k, keys));
        Assert.Contains("foreach (var key in PricingIndex.ScriptKeys)", View("PriceLists/_IndexL10n.cshtml"));
        foreach (var module in new[] { "PricingRules", "FxRates", "PricingHistory" })
        {
            Assert.Equal(View("PriceLists/_IndexL10n.cshtml"), View(module + "/_IndexL10n.cshtml"));          // one payload, every list module
            Assert.Equal(Js("PriceLists/index.l10n.js"), Js(module + "/index.l10n.js"));
        }
    }

    [Fact]
    public void D11_the_help_of_the_eight_codes_is_in_seven_languages_and_each_page_names_its_code()
    {
        foreach (var code in PricingIndex.HelpCodes)
        {
            foreach (var language in Languages)
            {
                var help = Resx("PageHelpResource." + language + ".resx");
                foreach (var key in new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, 4).SelectMany(n => new[] { "Step" + n, "Step" + n + "Title" })))
                {
                    Assert.True(help.TryGetValue($"Help.{code}.{key}", out var text) && !string.IsNullOrWhiteSpace(text), $"{language} Help.{code}.{key}");
                }

                var nav = Resx("SharedResource." + language + ".resx");
                Assert.True(nav.TryGetValue("Nav.Page." + code, out var label) && !string.IsNullOrWhiteSpace(label), $"{language} Nav.Page.{code}");
            }

            Assert.All(Enumerable.Range(1, 4), n => Assert.NotEqual(Diten.Web.Services.Help.PageHelpCatalog.DefaultStepIcon, Diten.Web.Services.Help.PageHelpCatalog.StepIcon(code, n)));
        }

        foreach (var (file, code) in new[] { ("PriceLists/Create.cshtml", "ListFormHelpCode"), ("PriceLists/Import.cshtml", "ImportHelpCode"), ("PricingRules/Index.cshtml", "RuleListHelpCode"),
                     ("PricingRules/Create.cshtml", "RuleFormHelpCode"), ("Pricing/Trial.cshtml", "TrialHelpCode"), ("PricingHistory/Index.cshtml", "HistoryHelpCode"), ("FxRates/Index.cshtml", "FxHelpCode") })
        {
            Assert.Contains("ViewData[\"PageCode\"] = PricingIndex." + code + ";", View(file));
        }

        Assert.DoesNotContain("ViewData[\"PageCode\"]", View("PriceLists/Index.cshtml"));                 // the menu page COMMERCIALPRICING itself
    }

    [Fact]
    public void D12_the_menu_page_and_the_gateway_routes_the_proxy_calls()
    {
        var repo = Repo();
        var manifest = File.ReadAllText(Path.Combine(repo, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Crm", "SelfRegistration", "CrmManifestProvider.cs"));
        Assert.Contains("new ModuleManifestPage(\"COMMERCIALPRICING\", \"Price Lists and Rules\", \"/Commercial/Pricing\", \"commercial.price-lists.read\"", manifest);
        Assert.All(CommercialPricingController.Keys.Where(k => k != ListsRead), k => Assert.Contains("\"" + k + "\"", manifest));
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "Commercial", "CommercialPricingController.cs"));
        Assert.Matches(@"\[Authorize\]\s*\[Route\(""Commercial/Pricing""\)\]", controller);

        var ocelot = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "gateway", "Diten.ApiGateway", "ocelot.json"))).RootElement.GetProperty("Routes").EnumerateArray()
            .Select(r => (Path: r.GetProperty("UpstreamPathTemplate").GetString()!, Methods: r.TryGetProperty("UpstreamHttpMethod", out var m) ? m.EnumerateArray().Select(x => x.GetString()!.ToUpperInvariant()).ToList() : []))
            .ToList();
        bool Routed(string path, string method) => ocelot.Any(r => r.Methods.Contains(method) && Regex.IsMatch(path, "^" + Regex.Replace(Regex.Escape(r.Path), @"\\\{[^}]+\}", "[^?]+") + "$", RegexOptions.IgnoreCase));
        foreach (var (method, path) in new[]
                 {
                     ("GET", "/api/commercial/price-lists"), ("POST", "/api/commercial/price-lists"), ("GET", "/api/commercial/price-lists/" + IdText), ("PUT", "/api/commercial/price-lists/" + IdText),
                     ("GET", "/api/commercial/price-lists/" + IdText + "/items"), ("POST", "/api/commercial/price-lists/" + IdText + "/decision"), ("POST", "/api/commercial/price-list-imports"),
                     ("GET", "/api/commercial/price-list-imports/" + IdText), ("POST", "/api/commercial/price-list-imports/" + IdText + "/convert"), ("GET", "/api/commercial/pricing-rules"),
                     ("POST", "/api/commercial/pricing-rules/trial"), ("PUT", "/api/commercial/pricing-rules/" + IdText), ("POST", "/api/commercial/pricing-rules/" + IdText + "/revisions"),
                     ("GET", "/api/commercial/fx-rates"), ("POST", "/api/commercial/fx-rates"), ("PUT", "/api/commercial/fx-rates/" + IdText), ("GET", "/api/commercial/pricing-history"),
                     ("GET", "/api/commercial/pricing-contract"), ("GET", "/api/legal-entities/lookup"), ("GET", "/api/product-master/stockable-items")
                 })
        {
            Assert.True(Routed(path, method), method + " " + path);
        }
    }

    // ═══ 8 · W6-FIX-b — tab words, the tabs' own sentences, the calendar's language ═════════════════════════════

    [Fact]
    public void D13_the_rules_tab_is_written_as_the_resource_writes_it_and_each_tab_says_its_own_sentence()
    {
        // V-12 — the Turkish label, and no capitalising transform on screen D's tab card (Sneat's nav-pills capitalize)
        Assert.Equal("MF / iskonto kuralları", Page("tr")["TabRules"]);
        Assert.Equal("MF / iskonto kuralları", Page("tr")["RulesTitle"]);
        Assert.Contains(".wcn-tabs .nav-link {\n    text-transform: none;\n}", Js("Pricing/pricing.css"));
        foreach (var page in new[] { "PriceLists/Index.cshtml", "PricingRules/Index.cshtml", "Pricing/Trial.cshtml", "PricingHistory/Index.cshtml", "FxRates/Index.cshtml" })
        {
            Assert.Contains("~/assets/js/Commercial/Pricing/pricing.css", View(page));                   // every tab page carries the fix
        }

        // G-1 — the price lists' sentence stays on the price lists tab; rules, history and rates say their own
        Assert.Contains("@Localizer[\"PageDescription\"]", View("PriceLists/Index.cshtml"));
        foreach (var (page, key) in new[] { ("PricingRules/Index.cshtml", "RulesDescription"), ("PricingHistory/Index.cshtml", "HistoryDescription"), ("FxRates/Index.cshtml", "FxDescription") })
        {
            Assert.DoesNotContain("@Localizer[\"PageDescription\"]", View(page));
            Assert.Contains("@Localizer[\"" + key + "\"]", View(page));
            foreach (var language in Languages) Assert.NotEqual(Page(language)["PageDescription"], Page(language)[key]);
        }
    }

    [Fact]
    public void D14_the_calendar_speaks_the_page_language_from_the_shared_date_field()
    {
        // G-2 — measured: no script gave flatpickr a locale; the shared date field localizes it once for <html lang>
        var shared = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "shared", "diten-datefield.js"));
        Assert.Contains("fp.localize(locale);", shared);
        Assert.Contains("<script src=\"~/assets/js/shared/diten-datefield.js\" asp-append-version=\"true\"></script>", View("Pricing/_PricingScripts.cshtml"));
        if (RunNode("process.stdout.write('ok')") is null) return;
        var path = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "shared", "diten-datefield.js").Replace("\\", "/");
        string Run(string lang) => RunNode("let given = null; globalThis.flatpickr = { localize: (l) => { given = l; } }; globalThis.document = { documentElement: { lang: '" + lang + "' } };"
            + "require('" + path + "'); process.stdout.write(JSON.stringify(given));")!;
        var tr = JsonDocument.Parse(Run("tr")).RootElement;
        Assert.Equal("Ocak", tr.GetProperty("months").GetProperty("longhand")[0].GetString());
        Assert.Equal("Ekim", tr.GetProperty("months").GetProperty("longhand")[9].GetString());
        Assert.Equal("Pazar", tr.GetProperty("weekdays").GetProperty("longhand")[0].GetString());     // flatpickr's lists start on Sunday
        Assert.Equal("Pazartesi", tr.GetProperty("weekdays").GetProperty("longhand")[1].GetString());
        Assert.Equal(1, tr.GetProperty("firstDayOfWeek").GetInt32());                                  // the Turkish week starts on Monday
        Assert.Equal("октябрь", JsonDocument.Parse(Run("ru")).RootElement.GetProperty("months").GetProperty("longhand")[9].GetString()!.ToLowerInvariant());
        Assert.Equal("null", Run("en"));                                                               // English stays flatpickr's own
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Json(object? value) => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        FileContentResult => 200,
        _ => null
    };

    private static (FakeGateway Gateway, CommercialPricingController Controller) Arrange(string[] permissions, string? answer = null, string? body = null)
    {
        var gateway = new FakeGateway(answer ?? "{\"data\":{}}");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new CommercialPricingController(new HttpClient(gateway), configuration, NullLogger<CommercialPricingController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", Me) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>(new NoTempData());
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (body is not null)
        {
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            http.Request.ContentType = "application/json";
        }

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
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

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private static string Core()
    {
        string P(string rel) => Path.Combine(JsRoot(), rel).Replace("\\", "/");
        return "globalThis.document = { documentElement: { lang: 'tr' }, getElementById: () => null };\n"
               + "require('" + P("commercial-rules.js") + "');\nrequire('" + P("PreOrders/preorders-core.js") + "');\nrequire('" + P("PreOrders/preorders-ui.js") + "');\n"
               + "const K = require('" + P("Pricing/pricing-core.js") + "');\nconst PU = require('" + P("Pricing/pricing-ui.js") + "');\n";
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "pr-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string JsRoot() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Commercial");

    private static string Js(string rel) => File.ReadAllText(Path.Combine(JsRoot(), rel)).Replace("\r\n", "\n");

    private static string View(string rel) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "Commercial", rel)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string relative)
        => XDocument.Load(Path.Combine(WebRoot(), "Resources", relative)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, string> Page(string culture)
        => Resx(Path.Combine("Views", "Commercial", "Pricing", "PricingIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx"));

    private static string Repo() => Path.GetDirectoryName(Path.GetDirectoryName(WebRoot()))!;

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}

internal static class PricingTestExtensions
{
    public static TResult Let<T, TResult>(this T value, Func<T, TResult> map) => map(value);
}
