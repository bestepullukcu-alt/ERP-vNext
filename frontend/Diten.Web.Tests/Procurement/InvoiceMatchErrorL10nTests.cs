using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.Procurement.InvoiceMatch;
using Diten.Web.Tests.Security;
using Diten.Web.Views.Procurement.InvoiceMatch;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Procurement;

/// <summary>
/// MOD-0143 invoice form: Procurement error codes (<c>Response.Fail</c>) are never shown raw. The capture form maps them to
/// page resources server-side; the Details actions map them in details.js from the page's L10n payload. Every mapped key
/// exists in all 7 tenant languages.
/// </summary>
public sealed class InvoiceMatchErrorL10nTests
{
    private const string Gateway = "http://gateway.test";

    private static readonly string[] Codes =
        ["VALIDATION_FAILED", "LINE_AMOUNT_MISMATCH", "UNKNOWN_REFERENCE", "CURRENCY_MISMATCH", "DUPLICATE_INVOICE", "INVALID_STATE", "NOT_FOUND"];

    private static readonly string[] Keys =
        ["ErrorValidationFailed", "ErrorLineAmountMismatch", "ErrorUnknownReference", "ErrorCurrencyMismatch", "ErrorDuplicateInvoice", "ErrorInvalidState", "ErrorNotFound"];

    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [InlineData("LINE_AMOUNT_MISMATCH", "page:ErrorLineAmountMismatch")]
    [InlineData("VALIDATION_FAILED", "page:ErrorValidationFailed")]
    [InlineData("DUPLICATE_INVOICE", "page:ErrorDuplicateInvoice")]
    [InlineData("SOMETHING_NEW", "shared:GatewayError")]
    public async Task Capture_refusal_shows_the_localized_text_not_the_code(string code, string expected)
    {
        var errors = await CaptureErrors(422, $"{{\"success\":false,\"errors\":[\"{code}\"]}}");

        Assert.Equal([expected], errors);
        Assert.DoesNotContain(errors, e => e.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Capture_non_json_body_and_403_never_reach_the_user_raw()
    {
        Assert.Equal(["shared:GatewayError"], await CaptureErrors(500, "upstream exploded at line 42"));
        Assert.Equal(["shared:AccessDenied"], await CaptureErrors(403, "{\"errors\":[\"FORBIDDEN\"]}"));
    }

    [Fact]
    public async Task Capture_transport_failure_hides_the_exception_message()
    {
        var errors = await CaptureErrors(handler: new Throwing());
        Assert.Equal(["shared:GatewayError"], errors);
    }

    [Fact]
    public async Task Lifecycle_proxy_failure_hides_the_exception_message()
    {
        var controller = Controller(new Throwing());
        var result = Assert.IsType<ObjectResult>(await controller.Match("INV-1"));
        Assert.Equal(502, result.StatusCode);
        var body = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("shared:GatewayError", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Throwing.Secret, body, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_mapped_key_exists_in_all_seven_languages_and_is_translated()
    {
        var root = ProxyHarness.RepoRoot();
        var dir = Path.Combine(root, "frontend", "Diten.Web", "Resources", "Views", "Procurement", "InvoiceMatch");
        var english = Values(Path.Combine(dir, "InvoiceMatchIndex.en.resx"));
        foreach (var lang in Languages)
        {
            var values = Values(Path.Combine(dir, $"InvoiceMatchIndex.{lang}.resx"));
            foreach (var key in Keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{lang}: {key} missing");
                if (lang != "en")
                    Assert.NotEqual(english[key], value);
            }
        }
    }

    [Fact]
    public void Controller_details_view_and_details_js_map_the_same_codes_to_the_same_keys()
    {
        var root = ProxyHarness.RepoRoot();
        var web = Path.Combine(root, "frontend", "Diten.Web");
        var controller = File.ReadAllText(Path.Combine(web, "Controllers", "InvoiceMatchController.cs"));
        var script = File.ReadAllText(Path.Combine(web, "wwwroot", "assets", "js", "Procurement", "InvoiceMatch", "details.js"));
        var view = File.ReadAllText(Path.Combine(web, "Views", "Procurement", "InvoiceMatch", "Details.cshtml"));

        var fromController = Regex.Matches(controller, "\\[\"([A-Z_]+)\"\\] = \"(Error[A-Za-z]+)\"").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
        var fromScript = Regex.Matches(script, "([A-Z_]+): '(Error[A-Za-z]+)'").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
        var expected = Codes.Zip(Keys).ToList();

        Assert.Equal(expected, fromController);
        Assert.Equal(expected, fromScript);
        foreach (var key in Keys)
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", view, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Values(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

    private static async Task<List<string>> CaptureErrors(int status = 200, string body = "", HttpMessageHandler? handler = null)
    {
        var controller = Controller(handler ?? new Fixed((HttpStatusCode)status, body));
        var model = new InvoiceMatchEditViewModel
        {
            SupplierId = "SUP-1", PoId = "PO-1", InvoiceNumber = "F-1", Currency = "TRY",
            Lines = [new InvoiceLineInput { ItemId = "I-1", Quantity = "2", UnitPrice = "10", LineAmount = "25" }]
        };

        Assert.IsType<ViewResult>(await controller.Create(model));
        return controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).ToList();
    }

    private static InvoiceMatchController Controller(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var controller = new InvoiceMatchController(new HttpClient(handler), configuration,
            new KeyLocalizer<SharedResource>("shared"), new KeyLocalizer<InvoiceMatchIndex>("page"), NullLogger<InvoiceMatchController>.Instance);
        var claims = new List<Claim> { new("tenantId", "66666666-6666-6666-6666-666666666666") };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private sealed class Fixed(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class Throwing : HttpMessageHandler
    {
        public const string Secret = "connection refused 10.0.0.7";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException(Secret);
    }

    private sealed class KeyLocalizer<T>(string prefix) : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, $"{prefix}:{name}");
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
