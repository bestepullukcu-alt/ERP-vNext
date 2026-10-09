using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.StrategyTemplates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-E2E-FIX-3 — the Web half, on the REAL controller and the REAL assets / resx:
/// <list type="bullet">
/// <item>E5-B1 "save + activate": the same permission rule as the activate endpoint (activate OR manage), a refused
/// activation reaches the shell as a localized warning (the shell now renders <c>WarningMessage</c>);</item>
/// <item>E5-B2 / E5-B3: the "Yerini v{n} aldı" badge and the "(arşivli)" name of an archived bound segment;</item>
/// <item>E1-B1 / E1-B3: the segment editor reads <c>{code,label}</c> reference items and pages the product list by 100;</item>
/// <item>E6-B1: the frequency form frames the band group, puts its error above it and focuses the first error.</item>
/// </list>
/// </summary>
public sealed class E2EFix3PlaySegmentFrequencyWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Manage = "crm.strategy-template.manage";
    private const string Activate = "crm.strategy-template.activate";
    private static readonly Guid TemplateId = Guid.Parse("7a7a7a7a-0000-4000-8000-000000000001");
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── Acceptance 3 — save + activate ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_and_activate_activates_with_the_manage_fallback_alone()
    {
        var gateway = new Gateway((method, uri) => method == HttpMethod.Put || uri.EndsWith("/activate")
            ? (HttpStatusCode.OK, """{"data":true,"isSuccessful":true}""")
            : (HttpStatusCode.NotFound, ""));
        var controller = Controller(gateway, Manage); // no crm.strategy-template.activate

        var result = await controller.Edit(TemplateId, Model(activate: true), default);

        Assert.Contains(gateway.Requests, r => r.Method == HttpMethod.Post && r.Uri.EndsWith($"/api/crm/strategy-templates/{TemplateId}/activate"));
        Assert.Equal("Details", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("RecordActivated", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task A_refused_activation_409_lands_as_a_localized_warning_with_its_reason()
    {
        var gateway = new Gateway((method, uri) => method == HttpMethod.Put
            ? (HttpStatusCode.OK, """{"data":true,"isSuccessful":true}""")
            : uri.EndsWith("/activate")
                ? (HttpStatusCode.Conflict, """{"data":false,"isSuccessful":false,"errors":["segment_not_active","Segment 'seg-1' is 'archived'."]}""")
                : (HttpStatusCode.NotFound, ""));
        var controller = Controller(gateway, Manage, Activate);

        var result = await controller.Edit(TemplateId, Model(activate: true), default);

        Assert.Equal("Details", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("SavedNotActivated P:ActivationBlocked_segment_not_active", controller.TempData["WarningMessage"]);
    }

    [Fact]
    public async Task A_refusal_without_a_known_code_still_says_why_generically()
    {
        var gateway = new Gateway((method, uri) => method == HttpMethod.Put
            ? (HttpStatusCode.OK, """{"data":true,"isSuccessful":true}""")
            : (HttpStatusCode.Conflict, """{"isSuccessful":false,"errors":["The strategy template is already active."]}"""));
        var controller = Controller(gateway, Manage);

        await controller.Edit(TemplateId, Model(activate: true), default);

        Assert.Equal("SavedNotActivated P:ActivationBlocked", controller.TempData["WarningMessage"]);
    }

    [Fact]
    public void The_shell_renders_the_warning_and_keeps_bare_resource_keys_silent()
    {
        var layout = Read("Views", "Shared", "_LayoutTenantShell.cshtml");
        Assert.Contains("warningMessage: @Json.Serialize(shellWarning)", layout);
        var shell = Read("wwwroot", "assets", "js", "backbone-shell.js");
        Assert.Contains("window.showToast(tempData.warningMessage, 'warning')", shell);

        // The guard the layout applies: a bare identifier (a resx key some pages store and never rendered) stays silent,
        // real text in any language is shown.
        var guard = Regex.Match(layout, "Regex\\.IsMatch\\(shellWarning, \"(?<p>[^\"]+)\"\\)").Groups["p"].Value;
        Assert.Matches(guard, "ArchivedTemplateReadOnly");
        Assert.DoesNotMatch(guard, "Kaydedildi, ancak etkinleştirilmedi. Bağlı bir segment aktif değil.");
        Assert.DoesNotMatch(guard, "已保存，但未激活。");
    }

    [Fact]
    public void Every_new_play_text_exists_in_seven_languages_with_turkish_diacritics()
    {
        var keys = new[]
        {
            "SupersededByVersion", "ArchivedSegmentSuffix", "ActivationBlocked", "ActivationBlocked_segment_not_active",
            "ActivationBlocked_segment_archived", "ActivationBlocked_segment_reference_not_found",
            "ActivationBlocked_frequency_policy_not_active", "ActivationBlocked_journey_not_published",
            "ActivationBlocked_journey_product_mismatch", "ActivationBlocked_product_line_journey_required",
            "ActivationBlocked_content_not_published", "ActivationBlocked_content_archived"
        };
        foreach (var language in Languages)
        {
            var resx = Resx(language);
            foreach (var key in keys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}:{key}");
            }

            Assert.Contains("{0}", resx["SupersededByVersion"]);
        }

        var tr = Resx("tr");
        Assert.Equal("Yerini v{0} aldı", tr["SupersededByVersion"]);
        Assert.Equal("(arşivli)", tr["ArchivedSegmentSuffix"]);
        Assert.Contains("değil", tr["ActivationBlocked_segment_not_active"]);
    }

    // ── E5-B2 badge + Acceptance 4 (E5-B3) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_list_and_details_show_replaced_by_version()
    {
        var index = Read("wwwroot", "assets", "js", "CRM", "StrategyTemplates", "index.js");
        Assert.Contains("row.supersededByTemplateVersion && L.SupersededByVersion", index);
        Assert.Contains("supersededLabel(row)", index);
        var details = Read("Views", "CRM", "StrategyTemplates", "Details.cshtml");
        Assert.Contains("Localizer[\"SupersededByVersion\", nextVersion]", details);
        Assert.Contains("\"SupersededByVersion\", \"ArchivedSegmentSuffix\"", Read("Views", "CRM", "StrategyTemplates", "_IndexL10n.cshtml"));
        Assert.NotNull(typeof(StrategyTemplateDetailViewModel).GetProperty(nameof(StrategyTemplateDetailViewModel.SupersededByTemplateVersion)));
    }

    [Fact]
    public void The_play_form_names_an_archived_bound_segment_and_offers_only_live_ones_for_a_new_binding()
    {
        var form = Read("wwwroot", "assets", "js", "CRM", "StrategyTemplates", "form.js");
        Assert.Contains("segments?includeArchived=true", form);
        Assert.DoesNotContain("segments?includeArchived=false", form);
        Assert.Contains("options.segmentAll = x; options.segment = x.filter(o => !o.archived);", form);
        Assert.Contains("const segmentById = id => (options.segmentAll || options.segment || []).find(o => o.id === id);", form);
        Assert.Contains("opt && opt.archived ? `${baseName} ${L.ArchivedSegmentSuffix", form);
        // the picker for a NEW binding still reads the live list
        Assert.Contains("const list = options.segment || [];", form);
    }

    // ── Acceptance 5 (E1-B1) + 6 (E1-B3) ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_segment_editor_reads_code_label_reference_items_backward_compatibly()
    {
        var form = Read("wwwroot", "assets", "js", "CRM", "Segments", "form.js");
        Assert.Contains("value: x.code ?? x.value ?? x.valueCode", form);
        Assert.Contains("text: x.label || x.text || x.displayName || x.code || x.value || x.valueCode", form);
        Assert.Contains("active: x.isActive !== false", form);
        Assert.Contains("(a.sortOrder - b.sortOrder)", form);
        Assert.Contains("options = toReferenceOptions(data?.items || data || []);", form);
        Assert.DoesNotContain(".filter(x => x.isActive !== false && (x.value || x.valueCode))", form);
    }

    [Fact]
    public void The_segment_product_list_is_read_in_pages_of_at_most_100()
    {
        var form = Read("wwwroot", "assets", "js", "CRM", "Segments", "form.js");
        Assert.Contains("'global-product': () => getAllPages('/global-products'),", form);
        Assert.Contains("const PICKER_PAGE_SIZE = 100;", form);
        Assert.Contains("pageSize=${PICKER_PAGE_SIZE}&pageNumber=${n}", form);
        Assert.DoesNotContain("global-products?pageSize=200", form);
    }

    // ── Acceptance 10 (E6-B1) ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_frequency_form_frames_the_band_group_and_focuses_the_first_error()
    {
        var form = Read("wwwroot", "assets", "js", "CRM", "VisitFrequencyPolicies", "form.js");
        Assert.Contains("vfpPriorityError: 'vfpBandCards'", form);
        Assert.Contains("const GROUP_FIELDS = ['vfpBandCards', 'vfpTargetTypeCards'];", form);
        Assert.Contains("'vfp-group-invalid' : 'is-invalid'", form);
        Assert.Contains("if (ERROR_FIELDS[id]) markField(ERROR_FIELDS[id], !!message);", form);
        Assert.Contains("focusFirstError(); return; }", form);
        Assert.Contains("first.scrollIntoView?.(", form);

        var editor = Read("Views", "CRM", "VisitFrequencyPolicies", "_Editor.cshtml");
        Assert.True(editor.IndexOf("id=\"vfpPriorityError\"", StringComparison.Ordinal)
                    < editor.IndexOf("id=\"vfpBandCards\"", StringComparison.Ordinal), "band error sits above the group");
        Assert.Contains(".vfp-create-scope .vfp-group-invalid", Read("wwwroot", "assets", "css", "visit-frequency-create.css"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static StrategyTemplatesController Controller(Gateway gateway, params string[] permissions)
    {
        var controller = new StrategyTemplatesController(new HttpClient(gateway), Configuration(), new KeyLocalizer(),
            NullLogger<StrategyTemplatesController>.Instance, new PageLocalizer());
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private static StrategyTemplateEditViewModel Model(bool activate) => new()
    {
        TemplateId = TemplateId,
        TemplateCode = "play-1",
        TemplateName = "Play 1",
        SubjectType = "contact",
        EffectiveFrom = DateTimeOffset.Parse("2026-10-01T00:00:00+00:00"),
        SegmentBindingsJson = "[]",
        FrequencyIntentJson = """{"mode":"none"}""",
        ProductLinesJson = "[]",
        ContentBindingsJson = "[]",
        ActivateAfterSave = activate
    };

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { WebRoot() }.Concat(parts).ToArray()));

    private static Dictionary<string, string> Resx(string language)
        => XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "StrategyTemplates", $"StrategyTemplatesIndex.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((request.Method, uri));
            var (status, payload) = route(request.Method, uri);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    /// <summary>Knows only the keys the page resx really defines (ResourceNotFound otherwise), prefixing "P:".</summary>
    private sealed class PageLocalizer : IStringLocalizer<StrategyTemplatesIndex>
    {
        private static readonly HashSet<string> Known = Resx("en").Keys.ToHashSet(StringComparer.Ordinal);
        public LocalizedString this[string name] => new(name, Known.Contains(name) ? "P:" + name : name, !Known.Contains(name));
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
