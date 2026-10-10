using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0290-FU04 S3b FIX1 (R1, R5) — MDM refuses a flagless submit with its code followed by the unset flags
/// (<c>["STOCK_FLAGS_REQUIRED_FOR_APPROVAL", "lotControlled"]</c>). The proxies say that as ONE sentence naming the flags in
/// the reader's language; a field name is never taken for a code (no "unmapped" warning, no status sentence beside it)
/// and never reaches the browser raw. The real controller actions are called; MDM is a scripted handler.
/// </summary>
public sealed class StockFlagsRefusalProxyTests
{
    private const string Code = "STOCK_FLAGS_REQUIRED_FOR_APPROVAL";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // The sabotage target "a field name counted as a code again".
    [Fact]
    public async Task A_gsku_refusal_with_its_flag_is_one_sentence_naming_the_flag_and_not_unmapped()
    {
        var result = await GskuSubmitAsync(Code, "lotControlled");

        var (status, errors, body) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("refusal:RefusalStockFlagsRequired[localized:StockFlagLotControlled]", Assert.Single(errors));
        Assert.DoesNotContain("unmapped", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"lotControlled", body, StringComparison.Ordinal);
    }

    // A flag name the proxy does not know is not shown raw; the sentence still names the GSKU's one flag.
    [Fact]
    public async Task A_gsku_refusal_with_an_unknown_detail_still_says_one_sentence()
    {
        var result = await GskuSubmitAsync(Code, "somethingNew");

        var (_, errors, body) = Read(result);
        Assert.Equal("refusal:RefusalStockFlagsRequired[localized:StockFlagLotControlled]", Assert.Single(errors));
        Assert.DoesNotContain("somethingNew", body, StringComparison.Ordinal);
        Assert.DoesNotContain("unmapped", body, StringComparison.Ordinal);
    }

    // Field-shaped items are details of the code before them — never a code of their own, even after a known code.
    [Theory]
    [InlineData("lotControlled")]
    [InlineData("gsku.lotControlled")]
    [InlineData("a")]
    [InlineData("X")]
    public async Task A_field_shaped_item_after_a_known_code_is_not_an_unmapped_code(string detail)
    {
        var result = await GskuSubmitAsync("GSKU_PARENT_NOT_APPROVED", detail);

        var (_, errors, body) = Read(result);
        Assert.Equal("localized:ErrorParentNotApproved", Assert.Single(errors));
        Assert.DoesNotContain("unmapped", body, StringComparison.Ordinal);
    }

    // S3b FIX2 (R-1): a list with no code in it (or an item before the first code) is not dropped — it takes the
    // unmapped path as before FIX1: the status sentence, flagged, the raw text never shown.
    // The sabotage target "a code-less list is dropped".
    [Theory]
    [InlineData("Some text")]
    [InlineData("lotControlled")]
    public async Task A_list_without_a_code_still_says_the_status_sentence_and_is_flagged(string item)
    {
        var result = await GskuSubmitAsync(item);

        var (status, errors, body) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(errors)));
        Assert.Contains("\"unmapped\":true", body, StringComparison.Ordinal);
        Assert.DoesNotContain(item, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_item_before_the_first_code_is_said_as_unmapped_and_the_code_after_it_too()
    {
        var result = await GskuSubmitAsync("lotControlled", "GSKU_PARENT_NOT_APPROVED");

        var (_, errors, body) = Read(result);
        Assert.Equal(2, errors.Length);
        Assert.Contains("localized:ErrorParentNotApproved", errors);
        Assert.Contains("\"unmapped\":true", body, StringComparison.Ordinal);
    }

    // S3b FIX2 (minor): a short upper-case detail ("ID") or a unit code ("C62") is not a code — no unmapped flag.
    [Theory]
    [InlineData("ID")]
    [InlineData("C62")]
    [InlineData("KGM")]
    public async Task A_short_upper_case_detail_after_a_known_code_is_not_a_code(string detail)
    {
        var result = await GskuSubmitAsync("GSKU_PARENT_NOT_APPROVED", detail);

        var (_, errors, body) = Read(result);
        Assert.Equal("localized:ErrorParentNotApproved", Assert.Single(errors));
        Assert.DoesNotContain("unmapped", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { "serialControlled", "gsku.lotControlled" },
        "ErrorStockFlagsRequired[StockFlagSerialControlled + StockFlagGskuLotControlled]")]
    [InlineData(new[] { "gsku.lotControlled" }, "ErrorStockFlagsRequired[StockFlagGskuLotControlled]")]
    [InlineData(new[] { "serialControlled" }, "ErrorStockFlagsRequired[StockFlagSerialControlled]")]
    public async Task An_lsku_refusal_names_which_flag_is_missing(string[] flags, string sentence)
    {
        var result = await LskuSubmitAsync([Code, .. flags]);

        var (status, errors, body) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(sentence, Assert.Single(errors));
        Assert.DoesNotContain(Code, body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"serialControlled", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"gsku.", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Gskus", "GskuRefusals", new[] { "RefusalStockFlagsRequired", "RefusalStockDaysRequireShelfLife" })]
    [InlineData("Gskus", "GskusIndex", new[] { "StockFlagLotControlled", "SubmitNeedsStockFlags", "StockDayRetest", "StockDayMinRemaining" })]
    [InlineData("Lskus", "LskusIndex", new[] { "ErrorStockFlagsRequired", "StockFlagSerialControlled", "StockFlagGskuLotControlled", "SubmitNeedsStockFlags" })]
    public void The_stock_flag_sentences_are_in_all_seven_languages_and_name_the_flags(string folder, string file, string[] keys)
    {
        foreach (var language in Languages)
        {
            var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                folder, $"{file}.{language}.resx");
            var values = XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(x => x.Attribute("name")!.Value, x => x.Element("value")?.Value ?? string.Empty);
            foreach (var key in keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{key} is missing in {language}");
                // The refusal sentences carry the flag list; a label or the reason never has a placeholder.
                Assert.Equal(Formatted(key), value!.Contains("{0}", StringComparison.Ordinal));
                // Well-formed: formatting with a label does not throw (a stray brace would, on the live screen).
                Assert.Contains("label", string.Format(System.Globalization.CultureInfo.InvariantCulture, value + " {0}", "label"));
            }
        }
    }

    // S5 (D-17): a 422 refusal (the stock edit's rules) stays a 422 with its sentence — never a 502.
    // The sabotage target "422 → 502".
    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, 422)]
    [InlineData(HttpStatusCode.Conflict, 409)]
    public async Task A_422_refusal_is_said_as_a_422(HttpStatusCode mdm, int expected)
    {
        var result = await GskuAnswerAsync(mdm, ["STOCK_DAYS_REQUIRE_SHELF_LIFE", "retestDays"]);

        var (status, errors, _) = Read(result);
        Assert.Equal(expected, status);
        Assert.Equal("refusal:RefusalStockDaysRequireShelfLife[localized:StockDayRetest]", Assert.Single(errors));
    }

    // S5 (D-18): the parity keys are the PRODUCTION map's — every refusal that names fields (GskusController.DetailSentences):
    // its sentence in GskuRefusals and each field label in GskusIndex, in all seven languages, the sentence with {0}.
    [Fact]
    public void Every_field_naming_refusal_of_the_proxy_is_in_all_seven_languages()
    {
        var map = (System.Collections.IDictionary)typeof(GskusController)
            .GetField("DetailSentences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        Assert.True(map.Count >= 2, "the proxy's field-naming refusals were not found");
        foreach (var language in Languages)
        {
            var refusals = Resx("Gskus", "GskuRefusals", language);
            var index = Resx("Gskus", "GskusIndex", language);
            foreach (System.Collections.DictionaryEntry entry in map)
            {
                var (key, labels) = ((string, IReadOnlyDictionary<string, string>))entry.Value!;
                Assert.True(refusals.TryGetValue(key, out var sentence) && sentence.Contains("{0}", StringComparison.Ordinal),
                    $"{key} missing or without {{0}} in {language}");
                foreach (var label in labels.Values)
                    Assert.True(index.TryGetValue(label, out var text) && !string.IsNullOrWhiteSpace(text), $"{label} missing in {language}");
            }
            foreach (var resx in new[] { index, Resx("Lskus", "LskusIndex", language) })
            {
                Assert.Matches(@"\{0\}.*\{1\}", resx["ListJoinTwo"]);
                Assert.False(string.IsNullOrEmpty(resx["ListSeparator"]), $"ListSeparator missing in {language}");
            }
        }
    }

    private static Dictionary<string, string> Resx(string folder, string file, string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                folder, $"{file}.{language}.resx")).Root!.Elements("data")
            .ToDictionary(x => x.Attribute("name")!.Value, x => x.Element("value")?.Value ?? string.Empty);

    // The refusal sentences that carry the field list.
    private static bool Formatted(string key) =>
        key.EndsWith("StockFlagsRequired", StringComparison.Ordinal) || key.EndsWith("RequireShelfLife", StringComparison.Ordinal);

    // S3b FIX2 (R-3): the days that need the shelf life first — one sentence naming them.
    [Theory]
    [InlineData(new[] { "retestDays" }, "refusal:RefusalStockDaysRequireShelfLife[localized:StockDayRetest]")]
    [InlineData(new[] { "minRemainingShelfLifeDays" }, "refusal:RefusalStockDaysRequireShelfLife[localized:StockDayMinRemaining]")]
    [InlineData(new[] { "retestDays", "minRemainingShelfLifeDays" },
        "refusal:RefusalStockDaysRequireShelfLife[localized:StockDayRetest + localized:StockDayMinRemaining]")]
    public async Task The_days_needing_the_shelf_life_are_one_sentence_naming_them(string[] fields, string sentence)
    {
        var result = await GskuSubmitAsync(["STOCK_DAYS_REQUIRE_SHELF_LIFE", .. fields]);

        var (_, errors, body) = Read(result);
        Assert.Equal(sentence, Assert.Single(errors));
        Assert.DoesNotContain("unmapped", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"retestDays", body, StringComparison.Ordinal);
    }

    private static Task<IActionResult> GskuSubmitAsync(params string[] errors) => GskuAnswerAsync(HttpStatusCode.Conflict, errors);

    private static async Task<IActionResult> GskuAnswerAsync(HttpStatusCode mdmStatus, string[] errors)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        var controller = new GskusController(new HttpClient(new Answer(Answered(mdmStatus, errors))), gateway,
            new EphemeralDataProtectionProvider(), new FormatLocalizer<Diten.Web.SharedResource>("localized"),
            new FormatLocalizer<GskusIndex>("localized"), NullLogger<GskusController>.Instance, new FormatLocalizer<GskuRefusals>("refusal"))
        { ControllerContext = new ControllerContext { HttpContext = Context("mdm.gskus.submit", new() { ["ExpectedVersion"] = "3" }) } };
        return await controller.SubmitLifecycle(Guid.NewGuid(), 3, CancellationToken.None);
    }

    private static async Task<IActionResult> LskuSubmitAsync(string[] errors)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.lsku.test" }).Build();
        var controller = new LskusController(new HttpClient(new Answer(Conflict(errors))), gateway,
            new EphemeralDataProtectionProvider(), new FormatLocalizer<LskusIndex>(null), NullLogger<LskusController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = Context("mdm.lskus.submit", new() { ["ExpectedVersion"] = "0" }) } };
        return await controller.SubmitLifecycle(Guid.NewGuid(), 0, CancellationToken.None);
    }

    private static HttpResponseMessage Conflict(string[] errors) => Answered(HttpStatusCode.Conflict, errors);

    private static HttpResponseMessage Answered(HttpStatusCode status, string[] errors) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { isSuccessful = false, statusCode = (int)status, errors }),
            Encoding.UTF8, "application/json")
    };

    private static (int Status, string[] Errors, string Body) Read(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        var body = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(body);
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToArray();
        return (objectResult.StatusCode!.Value, errors, body);
    }

    private static DefaultHttpContext Context(string permission, Dictionary<string, StringValues> form)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("permission", permission)
            ], "fixture"))
        };
        // Synthetic fixture credential: only seen by the scripted handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-stock-flags-refusal";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        form["__RequestVerificationToken"] = "synthetic-csrf";
        context.Request.Form = new FormCollection(form);
        return context;
    }

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }

    private sealed class Answer(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(response);
    }

    // Says each key back (with a prefix); the stock-flag sentences carry a format item, as the resx values do.
    private sealed class FormatLocalizer<T>(string? prefix) : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, Text(name));
        public LocalizedString this[string name, params object[] arguments] => new(name, Text(name));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        private string Text(string name)
        {
            // S5 (D-19): the join patterns are the resx's — here a visible stand-in.
            if (name == "ListJoinTwo") return "{0} + {1}";
            if (name == "ListSeparator") return "; ";
            var text = prefix is null ? name : $"{prefix}:{name}";
            return Formatted(name) ? text + "[{0}]" : text;
        }
    }
}
