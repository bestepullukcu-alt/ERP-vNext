using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-KP-5a-UI — the Safety Texts and Country Legal Profiles screens on the REAL controllers and assets: every proxy
/// endpoint reaches the right CRM path with the right verb, body and JWT tenant; the permission gates (read / manage /
/// submit) stop a call before the gateway; a rejection without a comment is refused by the proxy (and by the page
/// script); the decision panel exists only while CRM says canDecide; UAS-001 (no skeleton without the key); the text
/// preview never interprets HTML; the server-mode list contract; the seven-language resx families.
/// </summary>
public sealed class RegulatoryTextsWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Id = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static TheoryData<string> Kinds => new() { "safety", "legal" };

    // ============================================================ proxy → CRM path / verb / body

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Every_write_reaches_its_crm_path_with_its_verb_and_body(string kind)
    {
        var (gateway, controller, crm) = Arrange(kind, Read(kind), Manage(kind), Submit(kind));
        var body = JsonDocument.Parse("""{"body":"x","countryCode":"TR"}""").RootElement;

        await Create(controller, body);
        await Update(controller, body);
        await Call(controller, "SubmitItem");
        await Call(controller, "WithdrawItem");
        await Call(controller, "NewVersion");
        await Call(controller, "Archive");
        await Call(controller, "Get");

        Assert.Equal(
            [
                ("POST", $"{GatewayUrl}{crm}"),
                ("PUT", $"{GatewayUrl}{crm}/{Id}"),
                ("POST", $"{GatewayUrl}{crm}/{Id}/submit"),
                ("POST", $"{GatewayUrl}{crm}/{Id}/withdraw"),
                ("POST", $"{GatewayUrl}{crm}/{Id}/new-version"),
                ("POST", $"{GatewayUrl}{crm}/{Id}/archive"),
                ("GET", $"{GatewayUrl}{crm}/{Id}")
            ],
            gateway.Requests.Select(r => (r.Method.Method, r.Uri)).ToArray());
        Assert.Equal("""{"body":"x","countryCode":"TR"}""", gateway.Requests[0].Body);
        Assert.Equal("""{"body":"x","countryCode":"TR"}""", gateway.Requests[1].Body);
        Assert.All(gateway.Requests, r => Assert.Equal(TenantId.ToString(), r.Tenant));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_reader_cannot_write_or_submit_and_never_reaches_the_gateway(string kind)
    {
        var (gateway, controller, _) = Arrange(kind, Read(kind));
        var body = JsonDocument.Parse("{}").RootElement;

        Assert.Equal(403, Status(await Create(controller, body)));
        Assert.Equal(403, Status(await Update(controller, body)));
        Assert.Equal(403, Status(await Call(controller, "SubmitItem")));
        Assert.Equal(403, Status(await Call(controller, "WithdrawItem")));
        Assert.Equal(403, Status(await Call(controller, "NewVersion")));
        Assert.Equal(403, Status(await Call(controller, "Archive")));
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_manager_without_the_submit_key_cannot_submit(string kind)
    {
        var (gateway, controller, _) = Arrange(kind, Read(kind), Manage(kind));

        Assert.Equal(403, Status(await Call(controller, "SubmitItem")));
        Assert.Equal(403, Status(await Call(controller, "WithdrawItem")));
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_client_supplied_tenant_id_is_refused(string kind)
    {
        var (gateway, controller, _) = Arrange(kind, Read(kind), Manage(kind));

        var result = await Create(controller, JsonDocument.Parse("""{"tenantId":"x"}""").RootElement);

        Assert.Equal(400, Status(result));
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_bodiless_204_stays_bodiless(string kind)
    {
        var (_, controller, _) = Arrange(kind, (_, _) => (HttpStatusCode.NoContent, ""), Read(kind), Manage(kind));

        Assert.Equal(204, Assert.IsType<StatusCodeResult>(await Call(controller, "Archive")).StatusCode);
    }

    [Fact]
    public async Task A_crm_refusal_is_passed_through_with_its_code()
    {
        const string refusal = """{"isSuccessful":false,"statusCode":409,"errors":["review_template_missing","No KP-REG-TR template."]}""";
        var (_, controller, _) = Arrange("safety", (_, _) => (HttpStatusCode.Conflict, refusal), Read("safety"), Submit("safety"));

        var result = Assert.IsType<ContentResult>(await Call(controller, "SubmitItem"));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(refusal, result.Content);
    }

    // ============================================================ decision (K1)

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_rejection_without_a_comment_is_refused_before_the_gateway(string kind)
    {
        var (gateway, controller, _) = Arrange(kind, Read(kind));

        var result = await Decide(controller, """{"outcome":"reject","comment":"   "}""");

        Assert.Equal(400, Status(result));
        Assert.Contains("rejection_comment_required", JsonSerializer.Serialize(((ObjectResult)result).Value));
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task An_unknown_outcome_is_refused(string kind)
    {
        var (gateway, controller, _) = Arrange(kind, Read(kind));

        Assert.Equal(400, Status(await Decide(controller, """{"outcome":"maybe"}""")));
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_decision_forwards_only_the_outcome_and_the_comment(string kind)
    {
        var (gateway, controller, crm) = Arrange(kind, Read(kind));

        await Decide(controller, """{"outcome":"Reject","comment":" Eksik uyarı ","tenantId":"x","decidedBy":"me"}""");
        await Decide(controller, """{"outcome":"approve"}""");

        Assert.Equal($"{GatewayUrl}{crm}/{Id}/decision", gateway.Requests[0].Uri);
        Assert.Equal(HttpMethod.Post, gateway.Requests[0].Method);
        var reject = JsonDocument.Parse(gateway.Requests[0].Body!).RootElement;
        Assert.Equal(["outcome", "comment"], reject.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("reject", reject.GetProperty("outcome").GetString());
        Assert.Equal("Eksik uyarı", reject.GetProperty("comment").GetString());
        var approve = JsonDocument.Parse(gateway.Requests[1].Body!).RootElement;
        Assert.Equal("approve", approve.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, approve.GetProperty("comment").ValueKind);
    }

    [Fact]
    public void The_page_script_refuses_a_rejection_without_a_comment_before_calling()
    {
        var script = Asset("CRM", "RegulatoryTexts", "details.js");
        Assert.Contains("const rejectCommentMissing = (outcome, comment) => outcome === 'reject' && !String(comment || '').trim();", script);
        // In the decision handler the check comes first and returns — the POST is never made.
        var handler = script[script.IndexOf("$('#rtDecisionPanel')?.addEventListener", StringComparison.Ordinal)..];
        var check = handler.IndexOf("if (rejectCommentMissing(outcome, comment))", StringComparison.Ordinal);
        var post = handler.IndexOf("/decision`", StringComparison.Ordinal);
        Assert.True(check > 0 && post > check, "the reject-comment check must run before the decision call");
        Assert.Contains("return;", handler[check..post]);
    }

    [Fact]
    public void The_decision_panel_exists_only_while_crm_says_can_decide()
    {
        var script = Asset("CRM", "RegulatoryTexts", "details.js");
        Assert.Contains("const allowed = model.canDecide === true && String(model.status) === 'in-review';", script);
        Assert.Contains("panel.classList.toggle('d-none', !allowed);", script);
        foreach (var module in new[] { "SafetyTexts", "LegalProfiles" })
        {
            var view = View(module, "Details.cshtml");
            // Hidden by default: nothing is drawn until CRM's canDecide arrives.
            Assert.Contains("""<div class="card border-warning mb-4 d-none" id="rtDecisionPanel">""", view);
        }
    }

    // ============================================================ text preview is never HTML

    [Fact]
    public void The_text_preview_is_written_as_plain_text_never_as_html()
    {
        var script = Asset("CRM", "RegulatoryTexts", "details.js");
        var preview = Regex.Match(script, @"const renderPreview = \(el, value\) => \{(?<body>[\s\S]*?)\n    \};").Groups["body"].Value;
        Assert.Contains("el.textContent = value || '';", preview);
        Assert.DoesNotContain("innerHTML", preview);
        Assert.DoesNotContain("insertAdjacentHTML", preview);
        // Every [data-preview] goes through renderPreview, and a decision comment is escaped.
        Assert.Contains("root.querySelectorAll('[data-preview]').forEach((el) => renderPreview(el, pick(model, el.dataset.preview)));", script);
        Assert.Contains("esc(d.comment)", script);
        // A "<script>" body therefore lands as characters: no other writer of a preview exists.
        Assert.Single(Regex.Matches(script, @"data-preview"));
    }

    // ============================================================ list (server mode)

    [Fact]
    public async Task The_list_pages_filters_searches_and_sorts_the_crm_rows()
    {
        var rows = string.Join(",", Enumerable.Range(1, 12).Select(i =>
            $$"""{"id":"{{Guid.NewGuid()}}","safetyTextCode":"SAF-TR-{{i:000}}","globalProductId":"{{(i % 2 == 0 ? "p-even" : "p-odd")}}","globalProductCodeDisplay":"GP-{{i % 2}}","countryCode":"TR","languageCode":"tr","version":{{i}},"status":"{{(i <= 3 ? "active" : "draft")}}","canEdit":{{(i > 3 ? "true" : "false")}}}"""));
        var (gateway, controller, crm) = Arrange("safety", (_, _) => (HttpStatusCode.OK, $$$"""{"data":{"items":[{{{rows}}}]}}"""), Read("safety"));
        controller.ControllerContext.HttpContext.Request.QueryString =
            new QueryString("?start=0&length=2&orderBy=version&orderDir=desc&productId=p-even&status=draft&search=saf-tr&includeArchived=true&tenantId=x");

        var data = Data(await ((SafetyTextsController)controller).List(default));

        Assert.Equal($"{GatewayUrl}{crm}?includeArchived=true", Assert.Single(gateway.Requests).Uri);
        Assert.Equal(12, data.GetProperty("total").GetInt32());
        Assert.Equal(5, data.GetProperty("filteredTotal").GetInt32());   // even 4..12 that are drafts: 4,6,8,10,12
        Assert.Equal([12, 10], data.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("version").GetInt32()).ToArray());
        Assert.True(data.GetProperty("items")[0].GetProperty("canEdit").GetBoolean());
        Assert.Equal("SAF-TR-012", data.GetProperty("items")[0].GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("?orderBy=body")]
    [InlineData("?orderDir=up")]
    [InlineData("?length=501")]
    [InlineData("?start=-1")]
    public async Task The_list_refuses_an_unknown_sort_or_page(string query)
    {
        var (gateway, controller, _) = Arrange("legal", Read("legal"));
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString(query);

        Assert.Equal(400, Status(await ((LegalProfilesController)controller).List(default)));
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Resolve_forwards_only_the_identity()
    {
        var (gateway, controller, crm) = Arrange("safety", Read("safety"));

        await ((SafetyTextsController)controller).Resolve("p1", "TR", "tr", default);

        Assert.Equal($"{GatewayUrl}{crm}/resolve?productId=p1&countryCode=TR&languageCode=tr", Assert.Single(gateway.Requests).Uri);
    }

    [Fact]
    public async Task The_product_picker_reads_the_whole_mdm_catalogue_or_fails_closed()
    {
        var (gateway, controller, _) = Arrange("safety", (_, uri) => uri.Contains("pageNumber=1")
            ? (HttpStatusCode.OK, """{"data":{"items":[{"id":"a","canonicalCode":"GP-A","globalProductName":"A"}],"totalCount":2}}""")
            : (HttpStatusCode.OK, """{"data":{"items":[{"id":"b","canonicalCode":"GP-B","globalProductName":"B"}],"totalCount":2}}"""),
            Read("safety"), "mdm.global-products.read");

        var data = Data(await ((SafetyTextsController)controller).Products(default));

        Assert.Equal(["GP-A", "GP-B"], data.EnumerateArray().Select(p => p.GetProperty("code").GetString() ?? "").ToArray());
        Assert.Equal(2, gateway.Requests.Count);

        var (_, down, _) = Arrange("safety", (_, _) => (HttpStatusCode.ServiceUnavailable, ""), Read("safety"), "mdm.global-products.read");
        var refused = Assert.IsType<ObjectResult>(await ((SafetyTextsController)down).Products(default));
        Assert.Equal(503, refused.StatusCode);
        Assert.Contains("dependency_unavailable", JsonSerializer.Serialize(refused.Value));
    }

    // ============================================================ pages — UAS-001

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Without_the_key_a_page_is_a_plain_403_with_no_skeleton(string kind)
    {
        var (gateway, controller, _) = Arrange(kind);

        foreach (var result in new[] { Page(controller, "Index"), Page(controller, "Create"), Page(controller, "Details", Id), await PageAsync(controller, "Edit", Id) })
        {
            Assert.Equal(403, Assert.IsType<StatusCodeResult>(result).StatusCode);
        }

        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Only_a_draft_opens_the_edit_page()
    {
        var (_, active, _) = Arrange("safety", (_, _) => (HttpStatusCode.OK, """{"data":{"status":"active","canEdit":false}}"""), Manage("safety"));
        var redirect = Assert.IsType<RedirectToActionResult>(await ((SafetyTextsController)active).Edit(Id, default));
        Assert.Equal("Details", redirect.ActionName);

        var (_, draft, _) = Arrange("safety", (_, _) => (HttpStatusCode.OK,
            """{"data":{"status":"draft","canEdit":true,"safetyTextCode":"SAF-TR-0001","version":2,"globalProductId":"a0000000-0000-0000-0000-00000000000a","countryCode":"TR","languageCode":"tr","body":"Metin"}}"""),
            Manage("safety"));
        var view = Assert.IsType<ViewResult>(await ((SafetyTextsController)draft).Edit(Id, default));
        var model = Assert.IsType<SafetyTextEditViewModel>(view.Model);
        Assert.Equal(("TR", "tr", "Metin", 2), (model.CountryCode, model.LanguageCode, model.Body, model.Version));
    }

    [Theory]
    [InlineData("SafetyTexts", "crm.safety-text.read")]
    [InlineData("LegalProfiles", "crm.country-legal-profile.read")]
    public void The_details_route_is_the_task_deep_link(string module, string read)
    {
        var type = module == "SafetyTexts" ? typeof(SafetyTextsController) : typeof(LegalProfilesController);
        Assert.Equal($"CRM/{module}", type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Single().Template);
        var details = type.GetMethod("Details")!.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single();
        Assert.Equal("{id:guid}", details.Template);
        Assert.Equal(read, type.GetField("Read")!.GetValue(null));
    }

    // ============================================================ L10n — 7 languages, bridged, nav

    [Theory]
    [InlineData("SafetyTexts", "SafetyTextsIndex")]
    [InlineData("LegalProfiles", "LegalProfilesIndex")]
    public void The_resx_family_has_the_same_non_empty_keys_in_seven_languages(string module, string family)
    {
        var english = Resx(module, family, "en");
        var reference = english.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(reference.Count > 70);
        foreach (var language in Languages)
        {
            var values = Resx(module, family, language);
            Assert.True(reference.SetEquals(values.Keys), $"{language}: missing [{string.Join(", ", reference.Except(values.Keys))}]");
            Assert.All(values, kv =>
            {
                Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{language}: {kv.Key} is empty");
                // A key that echoes itself is a missing translation — except a word whose English text IS the key and
                // that the language shares (fr "Actions", "Code", "Version").
                if (english[kv.Key] != kv.Key) Assert.NotEqual(kv.Key, kv.Value);
            });
        }

        Assert.Equal("Ülke", Resx(module, family, "tr")["Country"]);
        Assert.Equal("Bu ülke için Regülasyon onay şablonu tanımlı değil.", Resx(module, family, "tr")["Err_review_template_missing"]);
    }

    [Theory]
    [InlineData("SafetyTexts", "SafetyTextsIndex")]
    [InlineData("LegalProfiles", "LegalProfilesIndex")]
    public void Every_key_the_views_and_scripts_read_exists(string module, string family)
    {
        var keys = Resx(module, family, "en");
        var views = Directory.GetFiles(Path.Combine(WebRoot(), "Views", "CRM", module), "*.cshtml").Select(File.ReadAllText).ToList();
        var used = views.SelectMany(v => Regex.Matches(v, @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value)).Distinct().ToList();
        Assert.All(used, k => Assert.True(keys.ContainsKey(k), $"{module}: view reads '{k}' which the resx lacks"));

        // The bridge carries every L().X the shared scripts read (PascalCase after index.l10n.js).
        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", module, "_IndexL10n.cshtml"));
        var scripts = Asset("CRM", "RegulatoryTexts", "details.js") + Asset("CRM", "RegulatoryTexts", "form.js") + Asset("CRM", module, "index.js");
        var read = Regex.Matches(scripts, @"L\(\)\.([A-Za-z_]+)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.All(read, k => Assert.Matches($@"(?<![A-Za-z0-9_]){Regex.Escape(k)}\s*=", bridge));
        // Dynamic families the scripts build: Status_{s}, Err_{code}, DecisionOutcome_{o}.
        foreach (var k in new[] { "Status_draft", "Status_in_review", "Status_active", "Status_superseded", "Status_archived",
                     "Err_review_template_missing", "Err_rejection_comment_required", "Err_dependency_unavailable",
                     "DecisionOutcome_approve", "DecisionOutcome_reject" })
        {
            Assert.Matches($@"(?<![A-Za-z0-9_]){k}\s*=", bridge);
        }
    }

    [Fact]
    public void The_two_nav_pages_are_named_in_seven_languages_without_echo()
    {
        foreach (var language in Languages)
        {
            var path = Path.Combine(WebRoot(), "Resources", $"SharedResource.{language}.resx");
            var values = XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => ((string?)d.Element("value") ?? string.Empty).Trim());
            foreach (var key in new[] { "Nav.Page.SAFETYTEXTS", "Nav.Page.LEGALPROFILES" })
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}: {key}");
                Assert.NotEqual(key, value);
            }
        }
    }

    // ============================================================ helpers

    private static string Read(string kind) => kind == "safety" ? SafetyTextsController.Read : LegalProfilesController.Read;
    private static string Manage(string kind) => kind == "safety" ? SafetyTextsController.Manage : LegalProfilesController.Manage;
    private static string Submit(string kind) => kind == "safety" ? SafetyTextsController.Submit : LegalProfilesController.Submit;

    private static (Gateway, RegulatoryTextsControllerBase, string) Arrange(string kind, params string[] permissions) =>
        Arrange(kind, (_, _) => (HttpStatusCode.OK, """{"data":{}}"""), permissions);

    private static (Gateway, RegulatoryTextsControllerBase, string) Arrange(
        string kind, Func<HttpMethod, string, (HttpStatusCode, string)> route, params string[] permissions)
    {
        var gateway = new Gateway(route);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        RegulatoryTextsControllerBase controller = kind == "safety"
            ? new SafetyTextsController(new HttpClient(gateway), configuration, NullLogger<SafetyTextsController>.Instance)
            : new LegalProfilesController(new HttpClient(gateway), configuration, NullLogger<LegalProfilesController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        var crm = kind == "safety" ? "/api/crm/knowledge/safety-texts" : "/api/crm/knowledge/country-legal-profiles";
        return (gateway, controller, crm);
    }

    private static Task<IActionResult> Create(RegulatoryTextsControllerBase c, JsonElement body) => c switch
    {
        SafetyTextsController s => s.CreateItem(body, default),
        LegalProfilesController l => l.CreateItem(body, default),
        _ => throw new InvalidOperationException()
    };

    private static Task<IActionResult> Update(RegulatoryTextsControllerBase c, JsonElement body) => c switch
    {
        SafetyTextsController s => s.UpdateItem(Id, body, default),
        LegalProfilesController l => l.UpdateItem(Id, body, default),
        _ => throw new InvalidOperationException()
    };

    private static Task<IActionResult> Decide(RegulatoryTextsControllerBase c, string json)
    {
        var body = JsonDocument.Parse(json).RootElement;
        return c switch
        {
            SafetyTextsController s => s.Decide(Id, body, default),
            LegalProfilesController l => l.Decide(Id, body, default),
            _ => throw new InvalidOperationException()
        };
    }

    private static Task<IActionResult> Call(RegulatoryTextsControllerBase c, string action) =>
        (Task<IActionResult>)c.GetType().GetMethod(action)!.Invoke(c, [Id, CancellationToken.None])!;

    private static IActionResult Page(RegulatoryTextsControllerBase c, string action, params object[] args) =>
        (IActionResult)c.GetType().GetMethod(action, args.Select(a => a.GetType()).ToArray())!.Invoke(c, args)!;

    private static Task<IActionResult> PageAsync(RegulatoryTextsControllerBase c, string action, Guid id) =>
        (Task<IActionResult>)c.GetType().GetMethod(action, [typeof(Guid), typeof(CancellationToken)])!.Invoke(c, [id, CancellationToken.None])!;

    private static int Status(IActionResult result) => result switch
    {
        StatusCodeResult s => s.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        ContentResult c => c.StatusCode ?? 200,
        _ => throw new InvalidOperationException(result.GetType().Name)
    };

    private static JsonElement Data(IActionResult result)
    {
        var value = Assert.IsAssignableFrom<ObjectResult>(result).Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement.GetProperty("data");
    }

    private static string View(string module, string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", module, file));

    private static string Asset(params string[] parts) =>
        File.ReadAllText(Path.Combine([WebRoot(), "wwwroot", "assets", "js", .. parts]));

    private static Dictionary<string, string> Resx(string module, string family, string language) =>
        XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", module, $"{family}.{language}.resx")).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string? Tenant, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, uri, request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null, body));
            var (status, payload) = route(request.Method, uri);
            return new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }
}
