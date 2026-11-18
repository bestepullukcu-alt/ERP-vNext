using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Services.Help;
using Diten.Web.Tests.Traceability;
using Diten.Web.Views.Warehouse.WarehouseActivity;
using Diten.Web.Views.Warehouse.WarehouseExceptions;
using Diten.Web.Views.Warehouse.WarehouseIntegration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-7 — the three S3 monitoring screens (pack §8.23, §11.1, §11.5, §24; AC-36 screen part) rendered by the REAL Razor views, their resx families
/// measured on the real files and the proxies driven with a Gateway handler:
/// <list type="bullet">
/// <item>UAS-001: without its page key every S3 page draws <c>_AccessDenied</c> and nothing else — no heading, filter, table, card, button, redirect; the
/// Integration page action does not even read the status (sabotage (4): the table skeleton drawn for a reader without the key → red);</item>
/// <item>every S3 page view names the tenant shell; a partial sets no layout; the two lists are server-mode v2 tables, Integration is NOT a DataTable;</item>
/// <item>the Integration Monitor's four cards (r24, son bakış 02 F-9) each show their own values of the answer — every value on exactly one card — and the
/// card titles come from <c>Warehouse.Integration.Card.*</c> in all seven languages;</item>
/// <item>the three families: one key set in 7 languages, nothing empty, nothing left in English, the {n} holes equal, every key read is there (sabotage (3):
/// an S3 key deleted in one language → red); the nav keys; the page help (2 / 2 / 1 titled steps, no Immediate, no tour);</item>
/// <item>the proxies: the filters reach the service in the contract's words, an unknown kind or a range over 31 days never leaves the Web, the service's
/// Staging difference (and its null) passes unchanged.</item>
/// </list>
/// Display decisions only — InventoryService's [HasPermission] stays the authority (UAS-001 §4).
/// </summary>
public sealed class WarehouseS3ScreensTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ExceptionsIndex = "/Views/Warehouse/WarehouseExceptions/Index.cshtml";
    private const string ActivityIndex = "/Views/Warehouse/WarehouseActivity/Index.cshtml";
    private const string IntegrationIndex = "/Views/Warehouse/WarehouseIntegration/Index.cshtml";
    private const string WarehouseId = "a1111111-0000-4000-8000-000000000001";
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] S3Keys =
        [WarehousePermissionKeys.ExceptionsRead, WarehousePermissionKeys.ActivityRead, WarehousePermissionKeys.IntegrationMonitorRead];

    /// <summary>Every other warehouse key a supervisor holds.</summary>
    private static readonly string[] Others =
    [
        WarehousePermissionKeys.OutboundShipmentsRead, WarehousePermissionKeys.PickTasksRead, WarehousePermissionKeys.PackagesRead,
        WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.LocationRolesRead
    ];

    // What a page must not draw for a reader without its key.
    private static readonly string[] Skeleton =
        ["inlineFilterHost", "skeleton-loader", "data-dt-data-mode", "<h5 class=\"mb-0\">", "offcanvasDetailsPreview", "dt-warehouseexceptions", "dt-warehouseactivity",
         "integration-cards", "data-wh-card", "integration-error", "inventory-company-selector", "integration-refresh"];

    private const string Status = """
        {"eventQueue":{"pending":2,"delivered":1,"oldestPendingAgeSeconds":5400,"oldestPendingAtUtc":"2026-11-10T08:00:00Z"},
         "awaitingDispatch":{"count":4,"oldestReadyAtUtc":"2026-11-10T07:05:00Z"},
         "lastSync":{"lastShippedAtUtc":"2026-11-10T09:18:00Z"},
         "pendingConfirmations":{"putaway":5,"pick":6,"total":11},
         "asOfUtc":"2026-11-10T09:30:00Z","contractVersion":"v1"}
        """;

    // ── UAS-001, layout, list mode ────────────────────────────────────────────────────────────────────────

    public static readonly TheoryData<string, string> PageKeys = new()
    {
        { ExceptionsIndex, WarehousePermissionKeys.ExceptionsRead },
        { ActivityIndex, WarehousePermissionKeys.ActivityRead },
        { IntegrationIndex, WarehousePermissionKeys.IntegrationMonitorRead }
    };

    [Theory]
    [MemberData(nameof(PageKeys))]
    public async Task AC36_UAS001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string key)
    {
        var all = S3Keys.Concat(Others).Where(k => k != key).ToArray();
        var html = await RenderAsync(view, "en", null, all);

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var marker in Skeleton) Assert.False(html.Contains(marker, StringComparison.Ordinal), $"{view} draws {marker} for a reader without {key}");
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("location.href", html, StringComparison.Ordinal);
    }

    /// <summary>UAS-001: the Integration page action reads nothing for a reader without the key (the view then draws the explanation only).</summary>
    [Fact]
    public async Task AC36_UAS001_the_integration_page_reads_no_status_without_its_key()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/integration/status", 200, Status);
        var result = await Integration(gateway, S3Keys.Where(k => k != WarehousePermissionKeys.IntegrationMonitorRead).Concat(Others).ToArray()).Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.Model);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public void AC36_every_S3_page_view_names_the_tenant_shell_and_the_partials_set_no_layout()
    {
        var files = new[] { "WarehouseExceptions", "WarehouseActivity", "WarehouseIntegration" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")).ToList();
        Assert.Equal(12, files.Count);
        Assert.Equal(3, files.Count(f => !Path.GetFileName(f).StartsWith('_')));
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            if (Path.GetFileName(file).StartsWith('_')) Assert.DoesNotMatch(@"\bLayout\s*=", source);
            else Assert.Contains("Layout = \"_LayoutTenantShell\";", source, StringComparison.Ordinal);
        }

        // §11.5: no create / edit offcanvas on the read-only lists; Integration is cards only (r23) — no DataTable partial, no list script
        foreach (var folder in new[] { "WarehouseExceptions", "WarehouseActivity" })
            Assert.False(File.Exists(Path.Combine(Web, "Views", "Warehouse", folder, "_CreateEditOffcanvas.cshtml")), $"{folder}: no create offcanvas (§11.5)");
        Assert.False(File.Exists(Path.Combine(Web, "Views", "Warehouse", "WarehouseIntegration", "_DataTable.cshtml")), "Integration is not a DataTable (r23)");
        Assert.DoesNotContain("createList", File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Warehouse", "WarehouseIntegration", "index.js")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC36_the_two_lists_are_read_only_server_mode_v2_tables()
    {
        var exceptions = await RenderAsync(ExceptionsIndex, "en", null, WarehousePermissionKeys.ExceptionsRead);
        var activity = await RenderAsync(ActivityIndex, "en", null, WarehousePermissionKeys.ActivityRead);

        Assert.Contains("id=\"dt-warehouseexceptions\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", exceptions, StringComparison.Ordinal);
        Assert.Contains("id=\"dt-warehouseactivity\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", activity, StringComparison.Ordinal);
        foreach (var html in new[] { PageBody(exceptions), PageBody(activity) })
        {
            foreach (var control in new[] { "bulkActionBar", "__RequestVerificationToken", "offcanvasConfirm", "dt-checkboxes-select-all", "btn-primary\" id=\"btnSave" })
                Assert.False(html.Contains(control, StringComparison.Ordinal), $"a read-only S3 list draws {control}");
        }

    }

    // ── Integration Monitor: the four cards (AC-36 "kartların DOM testi") ─────────────────────────────────

    /// <summary>
    /// AC-36 (r24, son bakış 02 F-9): the page action reads the status AFTER its key check and the view draws exactly four cards — Olay kuyruğu (Pending 2,
    /// Delivered 1, the oldest Pending's age 1 h 30 min), Sevk bekleyen (4, the oldest ReadyAt), Son eşitleme (the last Shipped), Bekleyen onaylar (5 + 6 = 11) —
    /// every value of the answer on exactly ONE card; no "loading" title anywhere.
    /// </summary>
    [Fact]
    public async Task AC36_the_four_cards_each_show_their_own_values_of_the_answer()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/integration/status", 200, Status);
        var keys = new[] { WarehousePermissionKeys.IntegrationMonitorRead };
        var view = Assert.IsType<ViewResult>(await Integration(gateway, keys).Index(CancellationToken.None));
        var model = Assert.IsType<WarehouseIntegrationViewModel>(view.Model);
        Assert.Single(gateway.Requests, r => r.Path == "/api/warehouse/integration/status");

        var html = await RenderAsync(IntegrationIndex, "en", model, keys);

        var cards = Regex.Matches(html, "data-wh-card=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["EventQueue", "AwaitingDispatch", "LastSync", "PendingConfirmations"], cards);
        var expected = new Dictionary<string, (string Value, string Shows)[]>
        {
            ["EventQueue"] = [("eventQueue.pending", ">2"), ("eventQueue.delivered", ">1"), ("eventQueue.oldestPendingAge", ">1 h 30 min")],
            ["AwaitingDispatch"] = [("awaitingDispatch.count", ">4"), ("awaitingDispatch.oldestReadyAt", "datetime=\"2026-11-10T07:05:00Z\"")],
            ["LastSync"] = [("lastSync.lastShippedAt", "datetime=\"2026-11-10T09:18:00Z\"")],
            ["PendingConfirmations"] = [("pendingConfirmations.putaway", ">5"), ("pendingConfirmations.pick", ">6"), ("pendingConfirmations.total", ">11")]
        };
        foreach (var (card, values) in expected)
        {
            var section = Card(html, card);
            foreach (var (value, shows) in values)
            {
                var cell = Between(section, $"data-wh-value=\"{value}\"", "</dd>").TrimEnd();
                if (shows.StartsWith('>')) Assert.EndsWith(shows, cell, StringComparison.Ordinal);   // the cell's text is exactly the value
                else Assert.Contains(shows, cell, StringComparison.Ordinal);
            }
        }

        // every value of the answer on exactly ONE card (the six values of §8.23 + the age and the two halves of the total)
        var all = expected.Values.SelectMany(v => v).Select(v => v.Value).ToList();
        foreach (var value in all) Assert.Single(Regex.Matches(html, $"data-wh-value=\"{Regex.Escape(value)}\""));
        Assert.Equal(all.Count, Regex.Matches(html, "data-wh-value=\"").Count);
        Assert.DoesNotMatch("(?i)loading|yükleniyor|spinner-border", PageBody(html));
    }

    /// <summary>AC-36: the four card titles come from Warehouse.Integration.Card.* in every language (sabotage: a card title key deleted in one language → red).</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public async Task AC36_the_card_titles_are_in_every_language(string culture)
    {
        var resx = Rows(Path.Combine(Web, "Resources", "Views", "Warehouse", "WarehouseIntegration", $"WarehouseIntegrationIndex.{culture}.resx"));
        var model = new WarehouseIntegrationViewModel { Status = JsonSerializer.Deserialize<WmsIntegrationStatusDto>(Status, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        var html = WebUtility.HtmlDecode(await RenderAsync(IntegrationIndex, culture, model, WarehousePermissionKeys.IntegrationMonitorRead));
        foreach (var card in WarehouseVocabulary.IntegrationCards)
        {
            var title = resx.GetValueOrDefault("Warehouse.Integration.Card." + card);
            Assert.False(string.IsNullOrWhiteSpace(title), $"{culture}: Warehouse.Integration.Card.{card}");
            Assert.Contains(title!, WebUtility.HtmlDecode(Card(html, card)), StringComparison.Ordinal);
        }
    }

    /// <summary>A failed read shows its localized reason and nothing else — never a card, never a "loading" title (the shared UI rule).</summary>
    [Fact]
    public async Task A_failed_status_read_shows_its_reason_and_no_card()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/integration/status", 503, """{"error":{"code":"INVENTORY_UNAVAILABLE","message":"x","correlationId":"c"},"contractVersion":"v1"}""");
        var keys = new[] { WarehousePermissionKeys.IntegrationMonitorRead };
        var model = Assert.IsType<WarehouseIntegrationViewModel>(Assert.IsType<ViewResult>(await Integration(gateway, keys).Index(CancellationToken.None)).Model);
        Assert.Equal("Warehouse.Error.INVENTORY_UNAVAILABLE", model.Error);

        var html = await RenderAsync(IntegrationIndex, "en", model, keys);
        Assert.Contains("integration-error", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wh-card", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("(?i)loading|spinner-border", PageBody(html));
    }

    // ── proxies ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The exception box proxy: kind + warehouse reach the service as the contract's words; the Staging difference and a deferral's null pass unchanged.</summary>
    [Fact]
    public async Task The_exception_proxy_passes_the_filters_and_the_staging_difference()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/exceptions", 200, $$$"""
            {"items":[
              {"exceptionId":"ShortDispatchUnresolved:o1:s1","kind":"ShortDispatchUnresolved","outboundId":"o1","warehouseId":"{{{WarehouseId}}}","openedAtUtc":"2026-11-10T08:00:00Z","status":"Open",
               "details":{"outboundNumber":"GMG-OUT-000001","skuId":"s1","expected":"20.000","issued":"20.000","reason":"OverIssued","stagingDifference":[{"lotId":"L1","onHand":"0.000","bound":"5.000","difference":"-5.000"}],"differenceDeferredBy":null}},
              {"exceptionId":"ShortDispatchUnresolved:o2:s1","kind":"ShortDispatchUnresolved","outboundId":"o2","warehouseId":"{{{WarehouseId}}}","openedAtUtc":"2026-11-10T08:00:00Z","status":"Open",
               "details":{"outboundNumber":"GMG-OUT-000002","skuId":"s1","expected":"30.000","issued":"25.000","reason":"LotMissing","stagingDifference":null,"differenceDeferredBy":"UnsyncedReadyToShip"}}],
             "total":2,"page":1,"pageSize":25,"contractVersion":"v1"}
            """);
        var controller = Exceptions(gateway, [WarehousePermissionKeys.ExceptionsRead]);
        controller.HttpContext.Request.QueryString = new QueryString($"?draw=3&start=0&length=25&kind=ShortDispatchUnresolved&warehouseId={WarehouseId}");

        var body = Body(await controller.List(CancellationToken.None));

        var call = Assert.Single(gateway.Requests, r => r.Path.StartsWith("/api/warehouse/exceptions", StringComparison.Ordinal));
        Assert.Equal($"/api/warehouse/exceptions?page=1&pageSize=25&kind=ShortDispatchUnresolved&warehouseId={WarehouseId}", call.Path);
        var items = body.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(("GMG-OUT-000001", "-5.000", JsonValueKind.Null), (items[0].GetProperty("record").GetString(),
            items[0].GetProperty("differences")[0].GetProperty("difference").GetString(), items[0].GetProperty("deferredBy").ValueKind));
        Assert.Equal((JsonValueKind.Null, "UnsyncedReadyToShip"), (items[1].GetProperty("differences").ValueKind, items[1].GetProperty("deferredBy").GetString()));
        Assert.Equal(3, body.GetProperty("draw").GetInt32());

        // a kind outside the nine never leaves the Web
        var wrong = Exceptions(gateway, [WarehousePermissionKeys.ExceptionsRead]);
        wrong.HttpContext.Request.QueryString = new QueryString("?kind=OutboundDrift");
        Assert.Equal(400, Assert.IsType<JsonResult>(await wrong.List(CancellationToken.None)).StatusCode);
        Assert.Single(gateway.Requests, r => r.Path.StartsWith("/api/warehouse/exceptions", StringComparison.Ordinal));
    }

    /// <summary>
    /// The activity proxy (§8.23 range ≤ 31 days): no range = the last 7 days sent as a closed range; a day range is sent as its whole days; 32 days, a
    /// reversed range or one end alone → 400 with the page's own sentence and no Gateway call; the source filter is one of the four.
    /// </summary>
    [Fact]
    public async Task The_activity_proxy_sends_a_closed_range_of_at_most_31_days()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/activity", 200, """{"items":[],"total":0,"page":1,"pageSize":25,"fromUtc":"x","toUtc":"y","contractVersion":"v1"}""");
        var keys = new[] { WarehousePermissionKeys.ActivityRead };

        var none = Activity(gateway, keys);
        none.HttpContext.Request.QueryString = new QueryString("?draw=1&start=0&length=25");
        Assert.IsType<JsonResult>(await none.List(CancellationToken.None));
        var sent = Assert.Single(gateway.Requests).Path;
        var from = DateTimeOffset.Parse(Uri.UnescapeDataString(Regex.Match(sent, "from=([^&]+)").Groups[1].Value), CultureInfo.InvariantCulture);
        var to = DateTimeOffset.Parse(Uri.UnescapeDataString(Regex.Match(sent, "to=([^&]+)").Groups[1].Value), CultureInfo.InvariantCulture);
        Assert.InRange((to - from).TotalSeconds, TimeSpan.FromDays(7).TotalSeconds - 2, TimeSpan.FromDays(7).TotalSeconds + 2);

        var days = Activity(gateway, keys);
        days.HttpContext.Request.QueryString = new QueryString("?from=2026-10-10&to=2026-11-09&kind=Movement");
        Assert.IsType<JsonResult>(await days.List(CancellationToken.None));
        Assert.Equal("/api/warehouse/activity?page=1&pageSize=25&from=2026-10-10T00%3A00%3A00Z&to=2026-11-09T23%3A59%3A59Z&kind=Movement", gateway.Requests[^1].Path);

        foreach (var (query, word) in new[] { ("?from=2026-10-01&to=2026-11-09", "RangeTooLong"), ("?from=2026-11-09&to=2026-11-01", "RangeOrder"), ("?from=2026-11-01", "RangeRequired") })
        {
            var before = gateway.Requests.Count;
            var bad = Activity(gateway, keys);
            bad.HttpContext.Request.QueryString = new QueryString(query);
            var refusal = Assert.IsType<JsonResult>(await bad.List(CancellationToken.None));
            Assert.Equal(400, refusal.StatusCode);
            Assert.Equal(word, Body(refusal).GetProperty("error").GetProperty("message").GetString());
            Assert.Equal(before, gateway.Requests.Count);
        }

        var source = Activity(gateway, keys);
        source.HttpContext.Request.QueryString = new QueryString("?kind=Shipment");
        Assert.Equal(400, Assert.IsType<JsonResult>(await source.List(CancellationToken.None)).StatusCode);
    }

    // ── localization and help (AC-23 pattern; tenant module → 7 languages) ─────────────────────────────────

    public static readonly TheoryData<string, string> Families = new()
    {
        { "WarehouseExceptions", "WarehouseExceptionsIndex" },
        { "WarehouseActivity", "WarehouseActivityIndex" },
        { "WarehouseIntegration", "WarehouseIntegrationIndex" }
    };

    /// <summary>Sabotage (3): an S3 key deleted in one language → this family is red.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    public void AC36_the_family_has_one_key_set_in_all_seven_languages_and_no_english_left_behind(string folder, string marker)
    {
        var family = Family(folder, marker);
        var en = family["en"];
        foreach (var lang in Languages)
        {
            Assert.Equal(en.Keys.Order(StringComparer.Ordinal), family[lang].Keys.Order(StringComparer.Ordinal));
            Assert.All(family[lang], kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty"));
            if (lang == "en") continue;
            var same = family[lang].Where(kv => kv.Value == en[kv.Key] && !Regex.IsMatch(kv.Value, @"^[\d{}\s:/.,–\-°C]*$")).Select(kv => kv.Key).ToList();
            Assert.True(same.Count == 0, $"{marker}.{lang} repeats the English text: {string.Join(", ", same)}");
        }

        foreach (var (key, value) in en)
        {
            var holes = Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Order().ToList();
            foreach (var lang in Languages) Assert.Equal(holes, Regex.Matches(family[lang][key], @"\{\d\}").Select(m => m.Value).Order().ToList());
        }
    }

    public static readonly TheoryData<string, string, string> Readers = new()
    {
        { "WarehouseExceptions", "WarehouseExceptionsIndex", "WarehouseExceptionsController.cs" },
        { "WarehouseActivity", "WarehouseActivityIndex", "WarehouseActivityController.cs" },
        { "WarehouseIntegration", "WarehouseIntegrationIndex", "WarehouseIntegrationController.cs" }
    };

    /// <summary>Every key a view, a controller or the script reads is in the family — the error keys, the vocabulary, the §8.23 kinds / deferrals / cards.</summary>
    [Theory]
    [MemberData(nameof(Readers))]
    public void AC36_every_key_a_view_a_controller_or_the_script_reads_is_there(string folder, string marker, string controller)
    {
        var en = Family(folder, marker)["en"];
        var views = Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value));
        var source = File.ReadAllText(Path.Combine(Web, "Controllers", controller));
        var controllerKeys = Regex.Matches(source, @"""(Col\w+|Field\w+|Range\w+|SystemActor)""").Select(m => m.Groups[1].Value);
        var vocabulary = folder switch
        {
            "WarehouseExceptions" => WarehouseVocabulary.ExceptionKeys,
            "WarehouseActivity" => WarehouseVocabulary.ActivityKeys,
            _ => WarehouseVocabulary.IntegrationKeys
        };

        var missing = views.Concat(controllerKeys).Concat(vocabulary).Concat(WarehouseErrorMessages.AllKeys).Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"{marker} lacks: {string.Join(", ", missing)}");
    }

    /// <summary>§11.4 (r25, son bakış 03 F-1; r24, son bakış 02 F-9): the nine kinds, the two DeferredBy values and the four card titles in all seven languages.</summary>
    [Fact]
    public void AC36_the_nine_kinds_the_two_deferrals_and_the_four_cards_are_in_all_seven_languages()
    {
        var exceptions = Family("WarehouseExceptions", "WarehouseExceptionsIndex");
        var integration = Family("WarehouseIntegration", "WarehouseIntegrationIndex");
        Assert.Equal(9, WarehouseVocabulary.ExceptionKinds.Length);
        Assert.Equal(4, WarehouseVocabulary.IntegrationCards.Length);
        foreach (var lang in Languages)
        {
            foreach (var kind in WarehouseVocabulary.ExceptionKinds)
            {
                Assert.False(string.IsNullOrWhiteSpace(exceptions[lang].GetValueOrDefault("Warehouse.Exception.Kind." + kind)), $"{lang}: Warehouse.Exception.Kind.{kind}");
                Assert.False(string.IsNullOrWhiteSpace(exceptions[lang].GetValueOrDefault("Warehouse.Exception.Hint." + kind)), $"{lang}: Warehouse.Exception.Hint.{kind}");
            }

            foreach (var deferral in new[] { "PendingConfirmation", "UnsyncedReadyToShip" })
                Assert.False(string.IsNullOrWhiteSpace(exceptions[lang].GetValueOrDefault("Warehouse.Exception.DeferredBy." + deferral)), $"{lang}: DeferredBy.{deferral}");
            foreach (var card in WarehouseVocabulary.IntegrationCards)
                Assert.False(string.IsNullOrWhiteSpace(integration[lang].GetValueOrDefault("Warehouse.Integration.Card." + card)), $"{lang}: Card.{card}");
        }
    }

    [Fact]
    public void AC36_the_S3_nav_keys_are_in_all_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var shared = Rows(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            foreach (var key in new[] { "Nav.Page.WAREHOUSEEXCEPTIONS", "Nav.Page.WAREHOUSEACTIVITY", "Nav.Page.WAREHOUSEINTEGRATION" })
                Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(key)), $"{lang}: {key}");
        }
    }

    public static readonly TheoryData<string, int, string> HelpPages = new()
    {
        { "WAREHOUSEEXCEPTIONS", 2, "Warehouse/WarehouseExceptions/Index.cshtml" },
        { "WAREHOUSEACTIVITY", 2, "Warehouse/WarehouseActivity/Index.cshtml" },
        { "WAREHOUSEINTEGRATION", 1, "Warehouse/WarehouseIntegration/Index.cshtml" }
    };

    /// <summary>UXH-001 (pack §24): purpose, audience, titled steps (2 / 2 / 1) in all seven languages, no Immediate, no tour.</summary>
    [Theory]
    [MemberData(nameof(HelpPages))]
    public void AC36_the_page_help_has_purpose_audience_and_titled_steps_in_all_seven_languages_and_no_tour(string code, int steps, string view)
    {
        foreach (var lang in Languages)
        {
            var help = Rows(Path.Combine(Web, "Resources", $"PageHelpResource.{lang}.resx"));
            var parts = new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, steps).SelectMany(n => new[] { $"Step{n}", $"Step{n}Title" }));
            foreach (var part in parts) Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.{part}")), $"{lang}: Help.{code}.{part}");
            Assert.False(help.ContainsKey($"Help.{code}.Step{steps + 1}"), $"{lang}: Help.{code} has more than {steps} steps");
            Assert.False(help.ContainsKey($"Help.{code}.Immediate"), $"{lang}: Help.{code} has no Immediate (read-only page)");
            Assert.All(help.Keys.Where(k => k.StartsWith($"Help.{code}.", StringComparison.Ordinal)),
                k => Assert.Matches($@"^Help\.{code}\.(Purpose|Audience|Step[1-5](Title)?|Related\.[A-Z]+)$", k));
            foreach (var related in PageHelpCatalog.RelatedCodes.GetValueOrDefault(code) ?? [])
                Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.Related.{related}")), $"{lang}: Help.{code}.Related.{related}");
        }

        for (var n = 1; n <= steps; n++) Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, n));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, steps + 1));
        Assert.Contains($"ViewData[\"PageCode\"] = \"{code}\";", File.ReadAllText(Path.Combine([Web, "Views", .. view.Split('/')])), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    [InlineData("tr", "ltr")]
    public async Task AC50_the_shell_turns_only_arabic_right_to_left_on_the_S3_pages(string culture, string direction)
    {
        foreach (var (view, key) in new[] { (ExceptionsIndex, WarehousePermissionKeys.ExceptionsRead), (ActivityIndex, WarehousePermissionKeys.ActivityRead) })
        {
            var html = await RenderAsync(view, culture, null, mainPage: true, key);
            Assert.Matches($"<html lang=\"{culture}\"[^>]*dir=\"{direction}\"", html);
        }
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The page's own part of a render (the layout around it has its own scripts and forms): from the page heading to the first script after it.</summary>
    private static string PageBody(string html)
    {
        var from = html.IndexOf("<h5 class=\"mb-0\">", StringComparison.Ordinal);
        Assert.True(from >= 0, "the page heading is not on the page");
        var to = html.IndexOf("<script", from, StringComparison.Ordinal);
        return to < 0 ? html[from..] : html[from..to];
    }

    private static string Card(string html, string card)
    {
        var start = html.IndexOf($"data-wh-card=\"{card}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"card {card} is not on the page");
        var next = html.IndexOf("data-wh-card=\"", start + 10, StringComparison.Ordinal);
        return next < 0 ? html[start..] : html[start..next];
    }

    private static Dictionary<string, string> Rows(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, Dictionary<string, string>> Family(string folder, string marker) =>
        Languages.ToDictionary(l => l, l => Rows(Path.Combine(Web, "Resources", "Views", "Warehouse", folder, $"{marker}.{l}.resx")));

    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"{start} is not on the page");
        var to = html.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        return to < 0 ? html[from..] : html[from..to];
    }

    private static JsonElement Body(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        return JsonDocument.Parse(JsonSerializer.Serialize(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();

    private static ControllerContext Context()
    {
        var claims = new List<Claim>
        {
            new("tenantId", "66666666-6666-6666-6666-666666666666"), new("sub", "55555555-0000-0000-0000-000000000001"),
            new("legal_entity_id", "77777777-7777-7777-7777-777777777777")
        };
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
    }

    private static WarehouseExceptionsController Exceptions(FakeGateway gateway, string[] keys) =>
        new(new HttpClient(gateway), Configuration(), new Permissions(keys), new KeyLocalizer<WarehouseExceptionsIndex>(), NullLogger<WarehouseExceptionsController>.Instance)
        { ControllerContext = Context() };

    private static WarehouseActivityController Activity(FakeGateway gateway, string[] keys) =>
        new(new HttpClient(gateway), Configuration(), new Permissions(keys), new KeyLocalizer<WarehouseActivityIndex>(), NullLogger<WarehouseActivityController>.Instance)
        { ControllerContext = Context() };

    private static WarehouseIntegrationController Integration(FakeGateway gateway, string[] keys) =>
        new(new HttpClient(gateway), Configuration(), new Permissions(keys), new KeyLocalizer<WarehouseIntegrationIndex>(), NullLogger<WarehouseIntegrationController>.Instance)
        { ControllerContext = Context() };

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    /// <summary>Answers /api/warehouse by the longest matching prefix (404 otherwise) and records what reached it; master-name reads are not recorded.</summary>
    private sealed class FakeGateway : HttpMessageHandler
    {
        private List<(string Prefix, int Status, string Body)> Routes { get; } = [];

        public List<(string Method, string Path)> Requests { get; } = [];

        public void On(string prefix, int status, string body) => Routes.Add((prefix, status, body));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (path.StartsWith("/api/warehouse", StringComparison.Ordinal))
                lock (Requests) Requests.Add((request.Method.Method, path));
            var route = Routes.Where(r => path.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            return Task.FromResult(route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") });
        }
    }

    private Task<string> RenderAsync(string viewPath, string culture, object? model, params string[] permissions) => RenderAsync(viewPath, culture, model, false, permissions);

    private async Task<string> RenderAsync(string viewPath, string culture, object? model, bool mainPage, params string[] permissions)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            // The token names its company, so the page needs no selector and makes no MDM call while rendering (W-0173-02).
            var claims = new List<Claim>
            {
                new("tenantId", Guid.NewGuid().ToString()), new("tenant_id", Guid.NewGuid().ToString()), new("actor_type", "tenant_user"),
                new("sub", Guid.NewGuid().ToString()), new("legal_entity_id", Guid.NewGuid().ToString())
            };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            var httpContext = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("tenant.test");
            httpContext.Request.Path = "/Warehouse/Exceptions";
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

            var engine = services.GetRequiredService<IRazorViewEngine>();
            var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: mainPage);
            if (!found.Success) throw new InvalidOperationException($"Could not load {viewPath}.");
            var actionContext = new ActionContext(httpContext, new RouteData { Values = { ["controller"] = "WarehouseExceptions", ["action"] = "Index" } },
                new ActionDescriptor(), new ModelStateDictionary());
            var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState) { Model = model };
            var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
            await using var writer = new StringWriter();
            await found.View.RenderAsync(new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions()));
            return writer.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        }
    }
}
