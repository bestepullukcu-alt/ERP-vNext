using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Services;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// WP-MVP1-FIX-QUI1 (live trial 2026-10-14, MOD-0175 Bloke Stok): (1) the "Bloke kaldırılsın mı?" dialog showed its body's raw
/// <c>&lt;div class=…&gt;</c>; (2) choosing a stock row overwrote a typed quantity with the whole row (50 → 200); (3) a closed record
/// stood in the list as "0 EA" with no status. The page behaviour is measured by Node against the shipped blocked.js
/// (<c>JavaScript/Node/quarantine-blocked.test.mjs</c>, run below); the door, the view and the words here.
/// </summary>
public sealed class QuarantineBlockedFixTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "http://gateway.test";
    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];

    /// <summary>The keys this WP adds — present, real and not English copies in all seven languages.</summary>
    public static readonly string[] NewKeys =
    [
        "Quarantine.BlockStock.Available",
        "Quarantine.Blocked.Filter.Status",
        "Quarantine.Blocked.Status.Open",
        "Quarantine.Blocked.Status.Closed",
        "Quarantine.Blocked.Status.SampleRetained",
        "Quarantine.Blocked.Status.All",
    ];

    private readonly WebApplicationFactory<Program> _factory;

    public QuarantineBlockedFixTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
    }

    // ── the page behaviour (Node, the shipped script) ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Bloke_Stok_page_holds_against_the_shipped_script()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "quarantine-blocked.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 11$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }

    // ── (3) the door: Open by default, the filter's values forwarded, the status shown travels back ────────────────────

    [Theory]
    [InlineData(null, "status=Open", "Open")]
    [InlineData("", "status=Open", "Open")]
    [InlineData("Closed", "status=Closed", "Closed")]
    [InlineData("SampleRetained", "status=SampleRetained", "SampleRetained")]
    [InlineData("All", null, "All")]
    public async Task The_blocked_list_asks_for_Open_by_default_and_forwards_the_status_filter(string? status, string? expected, string shown)
    {
        var gateway = new FakeGateway();
        var controller = Controller(gateway, status is null ? "?start=0&length=10" : $"?start=0&length=10&status={status}");

        var result = await controller.ListBlocked(CancellationToken.None);

        var sent = Assert.Single(gateway.Requests);
        Assert.StartsWith("/api/quarantine/blocked?", sent);
        var statuses = sent.Split('?')[1].Split('&').Where(p => p.StartsWith("status=", StringComparison.Ordinal)).ToList();
        if (expected is null) Assert.Empty(statuses);
        else Assert.Equal([expected], statuses);
        Assert.Equal(shown, Body(result).GetProperty("status").GetString());
    }

    // ── (2) + (3) the view ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_page_offers_the_status_filter_with_Open_first_and_the_available_hint_under_the_quantity()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Blocked.cshtml", null, "quarantine.holds.read", "quarantine.blocked.block");
        var select = Regex.Match(html, @"<select id=""filterStatus""[^>]*>([\s\S]*?)</select>");
        Assert.True(select.Success, "no status filter on the page");
        var options = Regex.Matches(select.Groups[1].Value, @"<option value=""([^""]*)"">([^<]*)</option>").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
        Assert.Equal([("", "Open"), ("Closed", "Closed"), ("SampleRetained", "Sample retained"), ("All", "All")], options);
        Assert.Contains("id=\"qr-block-available\"", html, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"id=""qr-block-uom""[\s\S]*?id=""qr-block-available""[\s\S]*?data-err=""qty"""), html);
    }

    // ── words ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_new_keys_are_delivered_and_real_in_all_seven_languages()
    {
        var keys = Diten.Web.Views.Quarantine.Quarantine.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.All(NewKeys, key => Assert.Contains(key, keys));
        var folder = Path.Combine(TraceViewRenderer.RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Quarantine");
        var en = Resx(Path.Combine(folder, "Quarantine.en.resx"));
        foreach (var lang in Languages)
        {
            var values = Resx(Path.Combine(folder, $"Quarantine.{lang}.resx"));
            foreach (var key in NewKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{lang}: {key} missing or empty");
                if (lang != "en") Assert.True(value != en[key], $"{lang}: {key} copies English");
            }

            Assert.Contains("{0}", values["Quarantine.BlockStock.Available"], StringComparison.Ordinal);
            Assert.Contains("{1}", values["Quarantine.BlockStock.Available"], StringComparison.Ordinal);
        }
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static JsonElement Body(IActionResult result) => result switch
    {
        ContentResult content => JsonDocument.Parse(content.Content!).RootElement,
        JsonResult json => JsonDocument.Parse(JsonSerializer.Serialize(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement,
        _ => throw new Xunit.Sdk.XunitException("unexpected result " + result.GetType().Name)
    };

    private static QuarantineController Controller(FakeGateway gateway, string query)
    {
        var controller = new QuarantineController(new Factory(gateway), new HttpClient(gateway),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build(),
            new Permissions(["quarantine.holds.read"]), new KeyLocalizer(), NullLogger<QuarantineController>.Instance);
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()), new("sub", Guid.NewGuid().ToString()), new("legal_entity_id", Guid.NewGuid().ToString()) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Headers.Cookie = "access_token=caller-token";
        http.Request.QueryString = new QueryString(query);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class KeyLocalizer : IStringLocalizer<Diten.Web.Views.Quarantine.Quarantine>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    /// <summary>Records path + query; answers an empty blocked page.</summary>
    private sealed class FakeGateway : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"items\":[],\"total\":0}", Encoding.UTF8, "application/json") });
        }
    }
}
