using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-E2E-FIX-2 — the knowledge-chain authoring screens on the Web layer (E2E TUTUKON findings): the content form's
/// product picker searches over the MDM selector instead of a 100-row preload (E2-B1), its type / status / source /
/// language options carry labels (E2-B3), a lost optimistic update is one localized sentence; the journey screens'
/// path-repeat badge (E4-B1), the stage defaults + coded publish errors + complete L10n list (E4-B2) and the
/// subject-scoped path list (E4-B3). Codes are read from the CRM source, labels from the REAL resx.
/// </summary>
public sealed class KnowledgeChainAuthoringFixWebTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid ProductId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ContentId = Guid.Parse("d0000000-0000-0000-0000-00000000000c");
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ============================================================ content form — product picker (acceptance 1)

    [Fact]
    public async Task The_content_form_no_longer_preloads_the_product_selector_and_shows_the_stored_product_by_name()
    {
        var gateway = new StubGateway();
        var view = Assert.IsType<ViewResult>(await Controller(gateway, "crm.knowledge.manage").Edit(ContentId, default));
        var model = Assert.IsType<KnowledgeContentEditViewModel>(view.Model);

        Assert.Null(model.ContractError);
        Assert.DoesNotContain(gateway.Requests, r => r.Contains("/api/global-products/selector"));   // no 100-row page
        Assert.Contains(gateway.Requests, r => r.EndsWith($"/api/global-products/{ProductId}"));      // one single read
        var stored = Assert.Single(model.ProductOptions);
        Assert.Equal((ProductId.ToString(), "TUTU-500 — TUTUKON 500 mg", false), (stored.Value, stored.Label, stored.IsInactive));

        var create = Assert.IsType<KnowledgeContentEditViewModel>(
            Assert.IsType<ViewResult>(await Controller(new StubGateway(), "crm.knowledge.manage").Create(default)).Model);
        Assert.Empty(create.ProductOptions);
    }

    [Fact]
    public void The_product_select_is_an_ajax_search_over_the_global_product_proxy()
    {
        var form = WebFile("Views", "CRM", "Knowledge", "_Form.cshtml");
        Assert.Contains("data-ajax-url=\"/CRM/Knowledge/api/global-product-options\"", form);
        var js = WebFile("wwwroot", "assets", "js", "CRM", "Knowledge", "form.js");
        Assert.Contains(".select2:not(:disabled):not([data-ajax-url])", js);     // never the static init
        Assert.Contains("url: el.dataset.ajaxUrl", js);
        Assert.Contains("data: params => ({ search: params.term || '', pageNumber: 1, pageSize: 100 })", js);
        Assert.Contains("setupProductPicker();", js);
        // The proxy the picker calls clamps to the MDM cap and forwards the search term.
        var action = typeof(KnowledgeController).GetMethod(nameof(KnowledgeController.GlobalProductOptions))!;
        Assert.Equal("api/global-product-options",
            ((HttpGetAttribute)action.GetCustomAttributes(typeof(HttpGetAttribute), false).Single()).Template);
    }

    // ============================================================ content form — labels (acceptance 2)

    [Fact]
    public void Every_content_type_status_and_source_code_has_a_label_in_all_seven_languages()
    {
        var domain = RepoFile("services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities", "KnowledgeContent.cs");
        var groups = new Dictionary<string, string>
        {
            ["KnowledgeContentTypes"] = "ContentType_",
            ["KnowledgeContentStatuses"] = "ContentStatus_",
            ["KnowledgeContentSources"] = "ContentSource_"
        };
        foreach (var (cls, prefix) in groups)
        {
            var codes = Constants(domain, cls);
            Assert.True(codes.Count >= 6, cls);
            foreach (var language in Languages)
            {
                var values = Resx("Knowledge", "KnowledgeIndex", language);
                foreach (var code in codes)
                {
                    Assert.True(values.TryGetValue(prefix + code, out var label) && !string.IsNullOrWhiteSpace(label),
                        $"{language}: {prefix}{code}");
                    Assert.NotEqual(code, label);                                     // a label, not the code
                }
            }
        }

        var tr = Resx("Knowledge", "KnowledgeIndex", "tr");
        Assert.Equal(("Broşür", "Taslak", "Manuel"), (tr["ContentType_brochure"], tr["ContentStatus_draft"], tr["ContentSource_manual"]));
    }

    [Fact]
    public void The_form_options_keep_the_code_as_value_and_show_the_label()
    {
        var form = WebFile("Views", "CRM", "Knowledge", "_Form.cshtml");
        Assert.Contains("<option value=\"@x\">@CodeLabel(\"ContentType_\", x)</option>", form);
        Assert.Contains("<option value=\"@x\">@CodeLabel(\"ContentStatus_\", x)</option>", form);
        Assert.Contains("<option value=\"@x\">@CodeLabel(\"ContentSource_\", x)</option>", form);
        Assert.Contains("<option value=\"@c\">@(ClaimDisplayNames.LanguageName(c) ?? c)</option>", form);
        Assert.DoesNotContain("<option value=\"@x\">@x</option>", form);
        Assert.DoesNotContain("<option value=\"@c\">@c</option>", form);
    }

    // ============================================================ content — lost optimistic write (acceptance 3, Web)

    [Fact]
    public async Task A_409_concurrency_conflict_is_one_localized_sentence_not_the_crm_text()
    {
        var gateway = new StubGateway
        {
            WriteStatus = 409,
            WriteErrors = ["knowledge_content_concurrency_conflict", "The content was modified by another writer; reload and retry."]
        };
        var controller = Controller(gateway, "crm.knowledge.manage");
        var model = Model();

        Assert.IsType<ViewResult>(await controller.Edit(ContentId, model, default));

        Assert.Equal("ContentConcurrencyConflict", model.WriteErrorKey);
        Assert.False(controller.ModelState.TryGetValue(string.Empty, out var summary) && summary!.Errors.Count > 0);
        Assert.Equal("knowledge_content_concurrency_conflict",
            Constant(RepoFile("services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities", "KnowledgeContent.cs"),
                "ContentConcurrencyConflict"));
        foreach (var language in Languages)
        {
            Assert.False(string.IsNullOrWhiteSpace(Resx("Knowledge", "KnowledgeIndex", language).GetValueOrDefault("ContentConcurrencyConflict")), language);
        }
    }

    // ============================================================ journeys — path repeat badge (E4-B1)

    [Fact]
    public void The_path_repeat_badge_is_named_as_such_and_differs_from_repeatable()
    {
        foreach (var language in Languages)
        {
            var values = Resx("ContentEngagementJourneys", "ContentEngagementJourneysIndex", language);
            Assert.NotEqual(values["Repeatable"], values["Repeated"]);
            Assert.False(string.IsNullOrWhiteSpace(values.GetValueOrDefault("RepeatedHint")), language);
        }

        var tr = Resx("ContentEngagementJourneys", "ContentEngagementJourneysIndex", "tr");
        Assert.Equal(("Yol tekrarı", "Yol başka aşamada da kullanılıyor"), (tr["Repeated"], tr["RepeatedHint"]));
        Assert.Contains("title=\"@Localizer[\"RepeatedHint\"]\">@Localizer[\"Repeated\"]", WebFile("Views", "CRM", "ContentEngagementJourneys", "Details.cshtml"));
    }

    // ============================================================ journeys — stage default + publish codes (acceptance 8)

    [Fact]
    public void A_new_stage_starts_required_and_an_edit_shows_the_stored_value()
    {
        var js = JourneyScript("form.js");
        Assert.Contains("document.getElementById('stageRequired').checked = stage ? !!stage.isRequired : true;", js);
    }

    [Fact]
    public void Every_journey_publish_code_shows_the_users_language_and_the_tr_text_for_no_required_stage()
    {
        var handlers = RepoFile("services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "Knowledge",
            "ContentEngagementJourney", "Handlers", "ContentEngagementJourneyCommandHandlers.cs");
        var publish = handlers[handlers.IndexOf("class PublishContentEngagementJourneyHandler", StringComparison.Ordinal)..];
        publish = publish[..publish.IndexOf("class CreateContentEngagementJourneyVersionHandler", StringComparison.Ordinal)];
        var domain = RepoFile("services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities", "ContentEngagementJourney.cs");
        var codes = Regex.Matches(publish, @"PublishFail\(ContentEngagementJourneyReasonCodes\.(\w+)")
            .Select(m => Constant(domain, m.Groups[1].Value)).Distinct().ToList();
        Assert.Equal(4, codes.Count);                                                  // archived, stale, V-J11, V-J10
        // No uncoded publish RULE left: only the request-shape answers (no tenant 400, unknown journey 404) stay plain.
        Assert.Equal(["Content engagement journey not found.", "Tenant context is required."],
            Regex.Matches(publish, @"Response<bool>\.Fail\(""([^""]+)""").Select(m => m.Groups[1].Value).Order().ToArray());
        Assert.Contains("content_engagement_journey_no_required_stage", codes);

        var l10n = WebFile("Views", "CRM", "ContentEngagementJourneys", "_IndexL10n.cshtml");
        foreach (var code in codes)
        {
            Assert.Contains($"\"Err_{code}\"", l10n);
            foreach (var language in Languages)
            {
                Assert.False(string.IsNullOrWhiteSpace(
                    Resx("ContentEngagementJourneys", "ContentEngagementJourneysIndex", language).GetValueOrDefault("Err_" + code)), $"{language}: {code}");
            }
        }

        Assert.Equal("Yolculuk yayımlanamaz: en az bir etkin ve zorunlu aşama gerekir. Bir aşamada “Zorunlu”yu açın.",
            Resx("ContentEngagementJourneys", "ContentEngagementJourneysIndex", "tr")["Err_content_engagement_journey_no_required_stage"]);
        var details = JourneyScript("details.js");
        Assert.Contains("errors.find(e => typeof e === 'string' && L['Err_' + e])", details);
        Assert.Contains("throw new Error(errorText(body))", details);
        // One meaningful question: the publish confirmation carries no generic sub-sentence.
        Assert.Contains("window.showConfirm?.(L.PublishConfirm || L.AreYouSure", details);
        Assert.Contains("}, { type: 'success', subtext: '' });", details);
    }

    // ============================================================ journeys — path list by subject (acceptance 9)

    [Fact]
    public void The_stage_path_list_request_carries_the_journeys_subject_and_language()
    {
        var js = JourneyScript("form.js");
        Assert.Contains("const journeySubjectId = editor.dataset.subjectId || '';", js);
        Assert.Contains("if (journeySubjectId) query += '&subjectId=' + encodeURIComponent(journeySubjectId);", js);
        Assert.Contains("query += '&language=' + encodeURIComponent(journeyLanguage);", js);
        Assert.Contains("fetch(`${endpoint}/knowledge-paths?${query}`", js);
        var form = WebFile("Views", "CRM", "ContentEngagementJourneys", "_Form.cshtml");
        Assert.Contains("data-subject-id=\"@subjectValue\"", form);
        Assert.Contains("data-language-code=\"@Model.LanguageCode\"", form);
        Assert.Contains("id=\"stagePathScopeNote\"", form);
        // CRM's list endpoint takes exactly these names.
        var api = RepoFile("services", "Diten.CrmService", "src", "Diten.CrmService.Api", "Controllers", "CRM", "KnowledgePathsController.cs");
        Assert.Contains("[FromQuery] Guid? subjectId", api);
        Assert.Contains("[FromQuery] string? language", api);
    }

    // ============================================================ journeys — L10n list is complete (acceptance 10)

    [Fact]
    public void The_journey_l10n_list_holds_every_key_the_scripts_read_in_all_seven_languages_and_reaches_the_editor()
    {
        var l10n = WebFile("Views", "CRM", "ContentEngagementJourneys", "_IndexL10n.cshtml");
        var listed = Regex.Matches(l10n[..l10n.IndexOf("};", StringComparison.Ordinal)], "\"([A-Za-z_]+)\"")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var used = new[] { "form.js", "details.js" }
            .SelectMany(s => Regex.Matches(JourneyScript(s), @"\bL\.([A-Za-z_]+)").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("PublishConfirm", used);
        Assert.Empty(used.Except(listed));
        foreach (var language in Languages)
        {
            var values = Resx("ContentEngagementJourneys", "ContentEngagementJourneysIndex", language);
            Assert.Empty(listed.Where(k => string.IsNullOrWhiteSpace(values.GetValueOrDefault(k))).Select(k => $"{language}:{k}"));
        }

        foreach (var page in new[] { "Edit.cshtml", "Create.cshtml" })
        {
            var view = WebFile("Views", "CRM", "ContentEngagementJourneys", page);
            var bridge = view.IndexOf("ContentEngagementJourneys/index.l10n.js", StringComparison.Ordinal);
            Assert.True(view.Contains("_IndexL10n.cshtml") && bridge > 0 && bridge < view.IndexOf("ContentEngagementJourneys/form.js", StringComparison.Ordinal), page);
        }
    }

    // ============================================================ helpers

    private static List<string> Constants(string source, string className)
    {
        var start = source.IndexOf($"public static class {className}", StringComparison.Ordinal);
        Assert.True(start >= 0, className);
        var body = source[start..source.IndexOf("IReadOnlyList<string> All", start, StringComparison.Ordinal)];
        return Regex.Matches(body, "public const string \\w+ = \"([^\"]+)\";").Select(m => m.Groups[1].Value).ToList();
    }

    private static string Constant(string source, string name)
    {
        var match = Regex.Match(source, $"public const string {name} = \"([^\"]+)\";");
        Assert.True(match.Success, name);
        return match.Groups[1].Value;
    }

    private static Dictionary<string, string> Resx(string folder, string family, string language)
        => XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", folder, $"{family}.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string JourneyScript(string name) => WebFile("wwwroot", "assets", "js", "CRM", "ContentEngagementJourneys", name);

    private static string WebFile(params string[] parts)
        => File.ReadAllText(Path.Combine([RepoRoot(), "frontend", "Diten.Web", .. parts]));

    private static string RepoFile(params string[] parts) => File.ReadAllText(Path.Combine([RepoRoot(), .. parts]));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static KnowledgeContentEditViewModel Model() => new()
    {
        ContentId = ContentId, ContentCode = "KC-2026-0001", ContentTitle = "TUTUKON leaflet", ContentType = "brochure",
        ContentStatus = "draft", SubjectId = Guid.NewGuid(), ProductId = ProductId, LanguageCode = "tr", ContentVersion = "1.0",
        EffectiveFrom = DateTimeOffset.UtcNow, Source = "manual", Url = "https://example.test/leaflet.pdf"
    };

    private static KnowledgeController Controller(StubGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new KnowledgeController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<KnowledgeController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private sealed class StubGateway : HttpMessageHandler
    {
        public int WriteStatus { get; init; } = 200;
        public string[] WriteErrors { get; init; } = [];
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add(uri);
            if (request.Method != HttpMethod.Get && uri.Contains("/api/crm/knowledge/contents"))
            {
                return Task.FromResult(Reply(WriteStatus, WriteStatus < 300
                    ? JsonSerializer.Serialize(new { data = true, isSuccessful = true, statusCode = WriteStatus })
                    : JsonSerializer.Serialize(new { data = (object?)null, isSuccessful = false, statusCode = WriteStatus, errors = WriteErrors })));
            }

            if (uri.EndsWith("/api/crm/knowledge/contract"))
            {
                return Task.FromResult(Reply(200, JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        isReady = true,
                        features = new { supportsKnowledgeContentManagement = true },
                        vocabularies = new
                        {
                            contentTypes = new[] { "presentation", "brochure" },
                            contentStatuses = new[] { "draft", "published" },
                            contentSources = new[] { "manual", "campaign" }
                        }
                    }
                })));
            }

            if (uri.EndsWith($"/api/global-products/{ProductId}"))
            {
                return Task.FromResult(Reply(200, JsonSerializer.Serialize(new
                {
                    data = new { id = ProductId, canonicalCode = "TUTU-500", globalProductName = "TUTUKON 500 mg" }
                })));
            }

            if (uri.EndsWith($"/api/crm/knowledge/contents/{ContentId}"))
            {
                return Task.FromResult(Reply(200, JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        contentId = ContentId, contentCode = "KC-1", contentTitle = "TUTUKON leaflet", contentType = "brochure",
                        contentStatus = "draft", subjectId = Guid.Empty, languageCode = "tr", contentVersion = "1.0",
                        effectiveFrom = DateTimeOffset.UtcNow, source = "manual", productId = ProductId
                    }
                })));
            }

            return Task.FromResult(Reply(200, "{\"data\":{\"items\":[]}}"));
        }

        private static HttpResponseMessage Reply(int status, string body) =>
            new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
