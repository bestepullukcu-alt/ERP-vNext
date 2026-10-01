using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.ContentSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-SB-1R — the Web side of retiring the ContentScope: the Content Studio create form posts the set's country +
/// language (no scope id), the country / content-language lookup comes from BRD with ICU names (503 when unreadable),
/// coded CRM context failures are shown in the user's language, the scope console / proxy is gone, and the new keys
/// exist in all 7 languages (the retired scope keys and the Content Scopes nav label do not).
/// </summary>
public sealed class ContentSetContextWebTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private const string CountryCodes =
        """{"data":{"items":[{"valueCode":"TR","displayName":"Turkey","sortOrder":10},{"valueCode":"UZ","displayName":"Uzbekistan","sortOrder":20},{"valueCode":"XX","displayName":"Old","isDeprecated":true}]}}""";

    private const string Languages =
        """{"data":{"items":[{"valueCode":"TR","attributes":{"Languages":"tr"}},{"valueCode":"UZ","attributes":{"Languages":"uz,ru"}}]}}""";

    // ================================================================ country / language lookup

    [Fact]
    public async Task Countries_list_the_brd_countries_with_their_content_languages_and_icu_names()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("tr");
            var json = await CountriesAsync(new StubGateway());

            var data = json.GetProperty("data").EnumerateArray().ToList();
            Assert.Equal(new[] { "TR", "UZ" }, data.Select(c => c.GetProperty("code").GetString()));   // deprecated XX dropped
            var uz = data[1];
            Assert.Equal("Özbekistan", uz.GetProperty("name").GetString());
            Assert.Equal(new[] { "uz", "ru" }, uz.GetProperty("languages").EnumerateArray().Select(l => l.GetProperty("code").GetString()));
            Assert.Equal("Русский", uz.GetProperty("languages")[1].GetProperty("nativeName").GetString());
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Countries_are_503_when_country_codes_cannot_be_read()
    {
        var result = await Controller(new StubGateway { CountryStatus = 500 }, "crm.content-set.read").Countries(default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        Assert.Contains("reference_set_unavailable", JsonSerializer.Serialize(obj.Value));
    }

    [Fact]
    public async Task Countries_need_content_set_read()
    {
        var result = await Controller(new StubGateway(), "crm.claim.read").Countries(default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ================================================================ create

    [Fact]
    public async Task Create_posts_country_and_language_and_no_scope()
    {
        var gateway = new StubGateway();
        var model = new ContentSetCreateViewModel
        {
            SetCode = "SET-1", SetName = "Set", ConceptChainTemplateId = Guid.NewGuid(), CountryCode = "TR", LanguageCode = "tr"
        };

        await Controller(gateway, "crm.content-set.manage").Create(model, default);

        using var doc = JsonDocument.Parse(gateway.Posts.Single());
        Assert.Equal("TR", doc.RootElement.GetProperty("countryCode").GetString());
        Assert.Equal("tr", doc.RootElement.GetProperty("languageCode").GetString());
        Assert.False(doc.RootElement.TryGetProperty("contentScopeId", out _));
    }

    [Theory]
    [InlineData("country_invalid")]
    [InlineData("language_not_in_country")]
    [InlineData("reference_set_unavailable")]
    public async Task A_coded_crm_context_failure_is_shown_in_the_user_language(string code)
    {
        var gateway = new StubGateway { WriteStatus = 400, WriteErrors = [code, "raw English message"] };
        var controller = Controller(gateway, "crm.content-set.manage");
        var model = new ContentSetCreateViewModel
        {
            SetCode = "SET-1", SetName = "Set", ConceptChainTemplateId = Guid.NewGuid(), CountryCode = "TR", LanguageCode = "en"
        };

        Assert.IsType<ViewResult>(await controller.Create(model, default));

        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("ContextError_" + code, error.ErrorMessage);          // the localizer key (KeyLocalizer echoes it)
    }

    // ================================================================ scope console removed

    [Fact]
    public void The_content_scope_console_and_its_proxy_are_gone()
    {
        Assert.Null(typeof(ContentSetsController).Assembly.GetType("Diten.Web.Controllers.CRM.ContentScopesController"));
        var routes = typeof(ContentSetsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .ToList();
        Assert.DoesNotContain(routes, r => r.Contains("scopes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("api/countries", routes);

        var web = Path.Combine(RepoRoot(), "frontend", "Diten.Web");
        Assert.False(Directory.Exists(Path.Combine(web, "Views", "CRM", "ContentScopes")));
        Assert.False(Directory.Exists(Path.Combine(web, "wwwroot", "assets", "js", "CRM", "ContentScopes")));
        Assert.False(Directory.Exists(Path.Combine(web, "Resources", "Views", "CRM", "ContentScopes")));
        var create = File.ReadAllText(Path.Combine(web, "Views", "CRM", "ContentSets", "Create.cshtml"));
        Assert.DoesNotContain("ContentScopeId", create);
        Assert.Contains("CountryCode", create);
        Assert.Contains("LanguageCode", create);
    }

    // ================================================================ L10n (7 languages)

    private static readonly string[] Cultures = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] NewKeys =
    [
        "Country", "Language", "Product", "Audience", "CountryPickerPlaceholder", "LanguagePickerPlaceholder",
        "ContextDerivedNote", "SaveContext", "ContextSaved"
    ];

    [Fact]
    public void Every_new_key_and_every_context_error_is_translated_in_all_7_languages()
    {
        var errorKeys = ContextErrorCodes().Select(c => "ContextError_" + c);
        var keys = NewKeys.Concat(errorKeys).ToList();
        var english = Values("ContentSets", "ContentSetsIndex", "en");
        foreach (var culture in Cultures)
        {
            var values = Values("ContentSets", "ContentSetsIndex", culture);
            foreach (var key in keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"ContentSetsIndex.{culture}.resx is missing '{key}'.");
                if (culture != "en")
                {
                    Assert.NotEqual(key, value);
                    Assert.True(value != english[key], $"ContentSetsIndex.{culture}.resx '{key}' is the English text.");
                }
            }

            foreach (var retired in new[] { "Scope", "NoScope", "ScopePickerPlaceholder", "ScopeOptionalNote" })
                Assert.False(values.ContainsKey(retired), $"ContentSetsIndex.{culture}.resx still carries '{retired}'.");

            var shared = XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", $"SharedResource.{culture}.resx"));
            Assert.DoesNotContain(shared.Root!.Elements("data"), d => (string?)d.Attribute("name") == "Nav.Page.CONTENTSCOPES");
        }
    }

    [Fact]
    public void The_workspace_bridge_carries_every_context_key_and_the_js_reads_them()
    {
        var web = Path.Combine(RepoRoot(), "frontend", "Diten.Web");
        var bridge = File.ReadAllText(Path.Combine(web, "Views", "CRM", "ContentSets", "_WorkspaceL10n.cshtml"));
        foreach (var key in new[] { "Country", "Language", "Product", "Audience", "SaveContext", "ContextSaved" }
                     .Concat(ContextErrorCodes().Select(c => "ContextError_" + c)))
        {
            Assert.Contains($"\"{key}\"", bridge);
        }

        var js = File.ReadAllText(Path.Combine(web, "wwwroot", "assets", "js", "CRM", "ContentSets", "workspace.js"));
        Assert.Contains("ContextError_", js);
        Assert.DoesNotContain("NoScope", js);
    }

    private static IReadOnlyList<string> ContextErrorCodes()
    {
        // Anchored to production: the CRM domain constants, not a copy of the list.
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src",
            "Diten.CrmService.Domain", "Entities", "ContentSet.cs"));
        var crm = Regex.Match(source, @"class ContentSetContextErrors\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;
        // WP-KP-1 moved the shared codes to ChainContextErrors; ContentSetContextErrors keeps them as aliases.
        var shared = Regex.Match(source, @"class ChainContextErrors\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;
        var sharedCodes = Regex.Matches(shared, "const string (?<name>\\w+)\\s*=\\s*\"(?<code>[^\"]+)\"")
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["code"].Value);
        var codes = Regex.Matches(crm, "=\\s*(?:\"(?<code>[^\"]+)\"|ChainContextErrors\\.(?<alias>\\w+))")
            .Select(m => m.Groups["code"].Success ? m.Groups["code"].Value : sharedCodes[m.Groups["alias"].Value])
            .ToList();
        Assert.Equal(5, codes.Count);
        codes.Add("component_language_mixed");   // the SB-2 release backstop (ContentSetReleaseErrors)
        return codes;
    }

    private static Dictionary<string, string> Values(string folder, string family, string culture)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", folder, $"{family}.{culture}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
    }

    // ================================================================ helpers

    private static async Task<JsonElement> CountriesAsync(StubGateway gateway)
    {
        var result = await Controller(gateway, "crm.content-set.read").Countries(default);
        var value = Assert.IsType<OkObjectResult>(result).Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();
    }

    private static ContentSetsController Controller(StubGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new ContentSetsController(new HttpClient(gateway), configuration, new KeyLocalizer<SharedResource>(),
            new KeyLocalizer<ContentSetsIndex>(), NullLogger<ContentSetsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class StubGateway : HttpMessageHandler
    {
        public int CountryStatus { get; init; } = 200;
        public int WriteStatus { get; init; } = 201;
        public string[] WriteErrors { get; init; } = [];
        public List<string> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Post)
            {
                Posts.Add(await request.Content!.ReadAsStringAsync(ct));
                var body = WriteStatus < 300
                    ? JsonSerializer.Serialize(new { data = Guid.NewGuid(), isSuccessful = true })
                    : JsonSerializer.Serialize(new { isSuccessful = false, errors = WriteErrors });
                return Reply(WriteStatus, body);
            }

            if (uri.Contains("/COUNTRY_CODES/")) return Reply(CountryStatus, CountryStatus == 200 ? CountryCodes : "{}");
            if (uri.Contains("/country-content-languages/")) return Reply(200, Languages);
            return Reply(200, "{\"data\":{\"items\":[]}}");
        }

        private static HttpResponseMessage Reply(int status, string body) =>
            new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
