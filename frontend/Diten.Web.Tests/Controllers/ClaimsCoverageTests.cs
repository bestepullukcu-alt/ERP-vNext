using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.Claims;
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
/// WP-CL-FE-2 — the coverage page gate (read; manage only drives the cell actions; no skeleton without read) and the
/// country / language display names of lookups/countries: ICU names in the request's UI culture, native names, the
/// unchanged <c>languages</c> code array (FE-3 reads it), and a code ICU does not know falling back instead of guessed.
/// </summary>
public sealed class ClaimsCoverageTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private const string CountryCodes =
        """{"data":{"items":[{"valueCode":"TR","displayName":"Turkey","sortOrder":10},{"valueCode":"UZ","displayName":"Uzbekistan","sortOrder":30},{"valueCode":"QQ","displayName":"Test land","sortOrder":90}]}}""";

    private const string Languages =
        """{"data":{"items":[{"valueCode":"TR","attributes":{"Languages":"tr"}},{"valueCode":"UZ","attributes":{"Languages":"uz,ru"}},{"valueCode":"QQ","attributes":{"Languages":"qqq"}}]}}""";

    // ============================================================ coverage page gate

    [Fact]
    public void Coverage_is_readable_with_crm_claim_read_and_flags_manage()
    {
        var reader = Assert.IsType<ViewResult>(Controller(Gateway(), "crm.claim.read").Coverage());
        Assert.Equal("~/Views/CRM/Claims/Coverage.cshtml", reader.ViewName);
        Assert.Equal(false, reader.ViewData["CanManageClaims"]);

        var manager = Assert.IsType<ViewResult>(Controller(Gateway(), "crm.claim.read", "crm.claim.manage").Coverage());
        Assert.Equal(true, manager.ViewData["CanManageClaims"]);
    }

    [Fact]
    public void Coverage_without_read_is_a_plain_403()
    {
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(Controller(Gateway(), "crm.claim.manage").Coverage()).StatusCode);
    }

    // ============================================================ display names

    [Theory]
    [InlineData("tr", "Özbekistan", "Özbekçe")]
    [InlineData("en", "Uzbekistan", "Uzbek")]
    [InlineData("fr", "Ouzbékistan", "Ouzbek")]
    [InlineData("ru", "Узбекистан", "Узбекский")]
    public async Task Country_and_language_names_follow_the_ui_culture(string culture, string country, string language)
    {
        var uz = await CountryAsync(culture, "UZ");

        Assert.Equal(country, uz.GetProperty("name").GetString());
        var detail = uz.GetProperty("languageDetails")[0];
        Assert.Equal("uz", detail.GetProperty("code").GetString());
        Assert.Equal(language, detail.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Native_names_are_in_the_countrys_own_language()
    {
        var uz = await CountryAsync("en", "UZ");

        Assert.Equal("Oʻzbekiston", uz.GetProperty("nativeName").GetString());
        var details = uz.GetProperty("languageDetails");
        Assert.StartsWith("O", details[0].GetProperty("nativeName").GetString()); // "o‘zbek" capitalised by its own culture
        Assert.Equal("Русский", details[1].GetProperty("nativeName").GetString());

        var tr = await CountryAsync("fr", "TR");
        Assert.Equal("Turquie", tr.GetProperty("name").GetString());
        Assert.Equal("Türkiye", tr.GetProperty("nativeName").GetString());
        Assert.Equal("Türkçe", tr.GetProperty("languageDetails")[0].GetProperty("nativeName").GetString());
    }

    [Fact]
    public async Task The_languages_code_array_stays_for_backward_compatibility()
    {
        var uz = await CountryAsync("tr", "UZ");

        Assert.Equal(JsonValueKind.Array, uz.GetProperty("languages").ValueKind);
        Assert.Equal(new[] { "uz", "ru" }, uz.GetProperty("languages").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task A_code_icu_does_not_know_falls_back_instead_of_being_guessed()
    {
        var qq = await CountryAsync("tr", "QQ");

        Assert.Equal("Test land", qq.GetProperty("name").GetString()); // the BRD display name, not "QQ" dressed up
        Assert.Equal("qqq", qq.GetProperty("languageDetails")[0].GetProperty("name").GetString());
    }

    // ============================================================ L10n

    [Fact]
    public void Coverage_keys_are_in_the_bridge_and_never_echo_in_any_language()
    {
        Assert.All(ClaimsIndexL10nKeys.CoverageKeys, k => Assert.Contains(k, ClaimsIndexL10nKeys.Bridge));
        foreach (var language in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Claims", $"ClaimsIndex.{language}.resx");
            var values = System.Xml.Linq.XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
            foreach (var key in ClaimsIndexL10nKeys.CoverageKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}: {key} missing");
                Assert.NotEqual(key, value);
            }
        }
    }

    // ============================================================ helpers

    private static async Task<JsonElement> CountryAsync(string culture, string code)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var result = await Controller(Gateway(), "crm.claim.read").V2LookupCountries(default);
            var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsAssignableFrom<ObjectResult>(result).Value)).RootElement;
            return json.GetProperty("data").EnumerateArray().Single(c => c.GetProperty("code").GetString() == code).Clone();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static StubGateway Gateway() => new(uri =>
        uri.Contains("/COUNTRY_CODES/") ? CountryCodes
        : uri.Contains("/country-content-languages/") ? Languages
        : "{\"data\":{\"items\":[]}}");

    private static ClaimsController Controller(StubGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new ClaimsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<ClaimsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
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

    private sealed class StubGateway(Func<string, string> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body(request.RequestUri!.ToString()), Encoding.UTF8, "application/json")
            });
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
