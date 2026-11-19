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
using Diten.Web.Views.Warehouse.OutboundShipments;
using Diten.Web.Views.Warehouse.Packing;
using Diten.Web.Views.Warehouse.PickTasks;
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
/// MOD-0178 DL-6 — the three S2 screens (pack §9, §11.1, §11.3, §11.5, §24; AC-33 (vi), AC-34, AC-45, AC-50) rendered by the REAL Razor views, their
/// resx families measured on the real files, and the operator flow driven through the REAL proxies with a Gateway handler:
/// <list type="bullet">
/// <item>UAS-001: without its page key every S2 page draws <c>_AccessDenied</c> and nothing else — no heading, table, form, panel, button, redirect;</item>
/// <item>every S2 page view names the tenant shell itself; a partial sets no layout (sabotage (cy));</item>
/// <item>the operator (WarehouseOperator's seven keys, no outbound read — §14.2, Q-10a) gets PickTasks with the confirm panel, the Packing page with
/// its package panel, and an iskeletsiz 403 on OutboundShipments; through the proxies: confirm → "Paketle" → Packing 200 → package → close → Packed
/// (sabotage (do): the Packing page key turned into the outbound read → red);</item>
/// <item>the three families + the S1 putaway family: one key set in 7 languages, nothing empty, nothing left in English, every key read is there, the
/// seven <c>Warehouse.Origin.*</c> values (sabotages (cz), (eh)), the nav keys, the page help (5 / 3 / 3 titled steps, no tour);</item>
/// <item>375 px (AC-45, DL-3 CT F-7 / F-8): every close × of a panel and the S2 controls carry the 48 px touch class; the bars stay at the bottom; a row
/// menu is capped to the screen and wraps.</item>
/// </list>
/// Display decisions only — InventoryService's [HasPermission] and state machine stay the authority (UAS-001 §4).
/// </summary>
public sealed class WarehouseS2ScreensTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string OutboundIndex = "/Views/Warehouse/OutboundShipments/Index.cshtml";
    private const string OutboundCreate = "/Views/Warehouse/OutboundShipments/Create.cshtml";
    private const string OutboundEdit = "/Views/Warehouse/OutboundShipments/Edit.cshtml";
    private const string OutboundDetails = "/Views/Warehouse/OutboundShipments/Details.cshtml";
    private const string PickIndex = "/Views/Warehouse/PickTasks/Index.cshtml";
    private const string PackingIndex = "/Views/Warehouse/Packing/Index.cshtml";
    private const string OutboundId = "c1111111-0000-0000-0000-000000000001";
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>WarehouseOperator (pack §14.2, AC-34): putaway-tasks.read / confirm, pick-tasks.read / confirm, packages.read / create / update.</summary>
    public static readonly string[] Operator =
    [
        "warehouse.putaway-tasks.read", "warehouse.putaway-tasks.confirm", WarehousePermissionKeys.PickTasksRead, WarehousePermissionKeys.PickTasksConfirm,
        WarehousePermissionKeys.PackagesRead, WarehousePermissionKeys.PackagesCreate, WarehousePermissionKeys.PackagesUpdate
    ];

    private static readonly string[] OutboundAll =
    [
        WarehousePermissionKeys.OutboundShipmentsRead, WarehousePermissionKeys.OutboundShipmentsCreate, WarehousePermissionKeys.OutboundShipmentsUpdate,
        WarehousePermissionKeys.OutboundShipmentsCancel, WarehousePermissionKeys.OutboundShipmentsRelease, WarehousePermissionKeys.PackagesCreate,
        WarehousePermissionKeys.PickTasksRead
    ];

    // What a page must not draw for a reader without its key.
    private static readonly string[] Skeleton =
        ["inlineFilterHost", "skeleton-loader", "data-dt-data-mode", "<h5 class=\"mb-0\">", "offcanvasDetailsPreview", "offcanvasConfirm", "offcanvasPackage",
         "bulkActionBar", "dt-outboundshipments", "dt-picktasks", "outboundForm", "outbound-details", "packing-root", "inventory-company-selector", "d-actions"];

    // ── UAS-001, layout, server mode ─────────────────────────────────────────────────────────────────────

    public static readonly TheoryData<string, string> PageKeys = new()
    {
        { OutboundIndex, WarehousePermissionKeys.OutboundShipmentsRead },
        { OutboundCreate, WarehousePermissionKeys.OutboundShipmentsCreate },
        { OutboundEdit, WarehousePermissionKeys.OutboundShipmentsUpdate },
        { OutboundDetails, WarehousePermissionKeys.OutboundShipmentsRead },
        { PickIndex, WarehousePermissionKeys.PickTasksRead },
        { PackingIndex, WarehousePermissionKeys.PackagesRead }
    };

    [Theory]
    [MemberData(nameof(PageKeys))]
    public async Task AC34_UAS001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string key)
    {
        var all = OutboundAll.Concat(Operator).Append(WarehousePermissionKeys.PickTasksUpdate).Distinct().Where(k => k != key).ToArray();
        var html = await RenderAsync(view, "en", all);

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var marker in Skeleton) Assert.False(html.Contains(marker, StringComparison.Ordinal), $"{view} draws {marker} for a reader without {key}");
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("location.href", html, StringComparison.Ordinal);
    }

    /// <summary>AC-34 / AC-21 (sabotage (cy)): every S2 page view names the tenant shell itself; a partial sets no layout.</summary>
    [Fact]
    public void AC34_every_S2_page_view_names_the_tenant_shell_and_the_partials_set_no_layout()
    {
        var files = new[] { "OutboundShipments", "PickTasks", "Packing" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")).ToList();
        // 15 → 16 (MOD-0178-FU01 DL-1, FU01 pack §11): the outbound line panel _LineOffcanvas.cshtml is a new PARTIAL — the page count below stays 6
        Assert.Equal(16, files.Count);
        Assert.Equal(6, files.Count(f => !Path.GetFileName(f).StartsWith('_')));
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            if (Path.GetFileName(file).StartsWith('_')) Assert.DoesNotMatch(@"\bLayout\s*=", source);
            else Assert.Contains("Layout = \"_LayoutTenantShell\";", source, StringComparison.Ordinal);
        }

        Assert.False(File.Exists(Path.Combine(Web, "Views", "Warehouse", "PickTasks", "_CreateEditOffcanvas.cshtml")), "§11.5: no create offcanvas on PickTasks");
    }

    [Fact]
    public async Task AC34_the_two_lists_are_server_mode_v2_tables()
    {
        var outbound = await RenderAsync(OutboundIndex, "en", WarehousePermissionKeys.OutboundShipmentsRead);
        var pick = await RenderAsync(PickIndex, "en", WarehousePermissionKeys.PickTasksRead);

        Assert.Contains("id=\"dt-outboundshipments\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", outbound, StringComparison.Ordinal);
        Assert.Contains("id=\"dt-picktasks\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", pick, StringComparison.Ordinal);
        Assert.DoesNotContain("bulkActionBar", outbound, StringComparison.Ordinal); // no bulk action on the outbound list (§11.3)
        foreach (var control in new[] { "offcanvasConfirm", "modalPickReason", "modalPickAssign", "pickActionToken", "bulkActionBar" })
            Assert.False(pick.Contains(control, StringComparison.Ordinal), $"a read-only pick reader gets {control}");
    }

    [Fact]
    public async Task The_Details_page_draws_each_dialog_only_for_its_key()
    {
        var read = await RenderAsync(OutboundDetails, "en", WarehousePermissionKeys.OutboundShipmentsRead);
        var all = await RenderAsync(OutboundDetails, "en", OutboundAll);

        // WP-0178-E3-FIX-01: "İptal" and "Sevke hazır" are window.showConfirm (MOD-0013) opened by details.js for a menu entry the reader's keys
        // allow — no page-drawn Bootstrap modal for anyone; the anti-forgery token the writes carry is drawn only with a key
        Assert.False(read.Contains("outboundActionToken", StringComparison.Ordinal), "a read-only reader gets the action token");
        Assert.Contains("outboundActionToken", all, StringComparison.Ordinal);
        foreach (var control in new[] { "modalOutboundCancel", "modalOutboundRelease" })
            Assert.False(all.Contains(control, StringComparison.Ordinal), $"the Details page still draws the Bootstrap modal {control}");

        Assert.Contains("data-outbound-id=\"" + OutboundId + "\"", all, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-34 operator flow, the screen half (sabotage (do)): the seven-key operator gets PickTasks with the confirm panel (no assign bar, no resolve), the
    /// Packing page with "Yeni koli" and the package panel and NO link to the outbound, and on OutboundShipments the explanation only (403, iskeletsiz).
    /// </summary>
    [Fact]
    public async Task AC34_the_operator_confirms_on_PickTasks_packs_on_Packing_and_gets_no_outbound_screen()
    {
        var pick = await RenderAsync(PickIndex, "tr", Operator);
        Assert.Contains("id=\"offcanvasConfirm\"", pick, StringComparison.Ordinal);
        Assert.Contains("data-wh-pack-link", pick, StringComparison.Ordinal);
        Assert.DoesNotContain("bulkActionBar", pick, StringComparison.Ordinal);
        Assert.DoesNotContain("modalPickAssign", pick, StringComparison.Ordinal);

        var packing = await RenderAsync(PackingIndex, "tr", Operator);
        Assert.DoesNotContain("diten-access-denied", packing, StringComparison.Ordinal);
        foreach (var control in new[] { "id=\"packing-root\"", "id=\"btnNewPackage\"", "id=\"offcanvasPackage\"", "packingActionToken" })
            Assert.Contains(control, packing, StringComparison.Ordinal);
        Assert.DoesNotContain("/Warehouse/OutboundShipments/Details/", packing, StringComparison.Ordinal);
        Assert.Contains("Paketleme", System.Net.WebUtility.HtmlDecode(packing), StringComparison.Ordinal);

        var outbound = await RenderAsync(OutboundIndex, "tr", Operator);
        Assert.Contains("diten-access-denied", outbound, StringComparison.Ordinal);
        foreach (var marker in Skeleton) Assert.False(outbound.Contains(marker, StringComparison.Ordinal), $"the operator sees {marker}");
    }

    /// <summary>
    /// AC-34 operator flow, the proxy half: with the seven keys only — the pick confirmation reaches the Gateway with the quantity only; the Packing read is
    /// ONE call to /outbound-shipments/{id}/packages (never /view — no ship-to); a package is opened and closed; the outbound reads Packed. The outbound
    /// proxy refuses the same reader before the Gateway.
    /// </summary>
    [Fact]
    public async Task AC34_the_operator_flow_runs_through_the_proxies_with_the_seven_keys()
    {
        var gateway = new FakeGateway();
        const string task = "d1111111-0000-0000-0000-000000000001";
        const string package = "e1111111-0000-0000-0000-000000000001";
        gateway.On($"/api/warehouse/pick-tasks/{task}/confirmations", 201,
            $$"""{"taskId":"{{task}}","number":"GMG-PCK-000001","status":"Completed","origin":"Outbound","outbound":{"outboundId":"{{OutboundId}}","outboundNumber":"GMG-OUT-000001","status":"Picked"},"version":3}""");
        gateway.On($"/api/warehouse/outbound-shipments/{OutboundId}/packages", 200,
            $$"""{"outbound":{"outboundId":"{{OutboundId}}","outboundNumber":"GMG-OUT-000001","status":"Packed","version":7},"allocations":[{"allocSeq":1,"item":{"itemId":"i","skuId":"s","skuLevel":"Lsku"},"picked":"30","packed":"30"}],"packages":[{"packageId":"{{package}}","packageNumber":"GMG-PKG-000001","outboundId":"{{OutboundId}}","status":"Closed","contents":[{"allocSeq":1,"quantity":"30"}],"version":2}]}""");
        gateway.On($"/api/warehouse/packages/{package}/close", 200,
            $$"""{"packageId":"{{package}}","packageNumber":"GMG-PKG-000001","outboundId":"{{OutboundId}}","status":"Closed","contents":[{"allocSeq":1,"quantity":"30"}],"version":2}""");

        var confirm = Body(await Pick(gateway, Operator).Confirm(task, new PickWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"2\"", Quantity = "30" }, CancellationToken.None));
        Assert.Equal("Picked", confirm.GetProperty("outbound").GetProperty("status").GetString());
        var sent = Assert.Single(gateway.Requests, r => r.Path.EndsWith("/confirmations", StringComparison.Ordinal));
        Assert.Equal(["channel", "idempotencyKey", "quantity"], JsonDocument.Parse(sent.Body).RootElement.EnumerateObject().Select(p => p.Name).Order());

        var close = Body(await Packing(gateway, Operator).Close(package, new PackageWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"1\"" }, CancellationToken.None));
        Assert.Equal("Closed", close.GetProperty("status").GetString());

        var read = Body(await Packing(gateway, Operator).Packages(OutboundId, CancellationToken.None));
        Assert.Equal("Packed", read.GetProperty("outbound").GetProperty("status").GetString());
        Assert.DoesNotContain(gateway.Requests, r => r.Path.Contains("/view", StringComparison.Ordinal));
        Assert.DoesNotContain("shipTo", read.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var before = gateway.Requests.Count;
        var refused = Assert.IsType<JsonResult>(await Outbound(gateway, Operator).List(CancellationToken.None));
        Assert.Equal(403, refused.StatusCode);
        Assert.Equal(before, gateway.Requests.Count);
    }

    // ── proxy bodies ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>r22: a 422 FEFO_BYPASS_REASON_REQUIRED comes back with its deviations (the dialog asks a reason each); the reasons go as fefoBypassReasons.</summary>
    [Fact]
    public async Task The_release_proxy_passes_the_FEFO_deviations_and_sends_the_reasons()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/outbound-shipments/{OutboundId}/release", 422,
            """{"error":{"code":"FEFO_BYPASS_REASON_REQUIRED","message":"x","details":{"deviations":[{"allocSeq":2,"lotId":"l","firstEligibleLotId":"f","firstEligibleExpiry":"2027-01-31"}]}}}""");

        var refused = Assert.IsType<JsonResult>(await Outbound(gateway, OutboundAll).Release(OutboundId, new OutboundWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"4\"" }, CancellationToken.None));
        Assert.Equal(422, refused.StatusCode);
        var error = Body(refused).GetProperty("error");
        Assert.Equal("Warehouse.Error.FEFO_BYPASS_REASON_REQUIRED", error.GetProperty("message").GetString());
        Assert.Equal(2, error.GetProperty("details").GetProperty("deviations")[0].GetProperty("allocSeq").GetInt32());

        await Outbound(gateway, OutboundAll).Release(OutboundId, new OutboundWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"4\"", FefoBypassReasons = [new OutboundReleaseReason { AllocSeq = 2, Reason = " a reason of ten " }]
        }, CancellationToken.None);
        var body = JsonDocument.Parse(gateway.Requests.Last().Body).RootElement;
        Assert.Equal("a reason of ten", body.GetProperty("fefoBypassReasons")[0].GetProperty("reason").GetString());
        Assert.Equal("W/\"4\"", gateway.Requests.Last().IfMatch);
    }

    /// <summary>The create body is the WMS shape: item { itemId, skuId, skuLevel }, the base unit, a lot change with its reason; Edit sends the header only.</summary>
    [Fact]
    public async Task The_create_body_is_the_wms_shape_and_an_edit_carries_the_header_only()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/outbound-shipments", 201, $$"""{"outboundId":"{{OutboundId}}","outboundNumber":"GMG-OUT-000001","status":"Allocated","version":1}""");
        var model = new OutboundWriteRequest
        {
            IdempotencyKey = Key, WarehouseId = OutboundId, OrderRef = " SO-9 ", ShipTo = new OutboundAddressRequest { Name = "Clinic", Country = "TR", City = "Ankara" },
            Lines = [new OutboundLineRequest { OrderLineId = "1", ItemId = OutboundId, SkuId = OutboundId, SkuLevel = "Item", UomId = "KG", Quantity = "30", LotOverride = new OutboundLotOverrideRequest { LotId = OutboundId, FefoBypassReason = "the customer asks this lot" } }]
        };
        var created = Body(await Outbound(gateway, OutboundAll).Create(model, CancellationToken.None));
        Assert.Equal("Warehouse.Outbound.Saved.Created", created.GetProperty("message").GetString());
        var body = JsonDocument.Parse(gateway.Requests.Single().Body).RootElement;
        Assert.Equal("SO-9", body.GetProperty("orderRef").GetString());
        var line = body.GetProperty("lines")[0];
        Assert.Equal("Item", line.GetProperty("item").GetProperty("skuLevel").GetString());
        Assert.Equal("the customer asks this lot", line.GetProperty("lotOverride").GetProperty("fefoBypassReason").GetString());
        Assert.False(body.GetProperty("shipTo").TryGetProperty("line1", out _)); // an empty optional member is not written

        Assert.Null(OutboundShipmentsController.EditBody(JsonDocument.Parse("""{"lines":[]}""").RootElement));
        Assert.Equal(["note", "orderRef"], OutboundShipmentsController.EditBody(JsonDocument.Parse("""{"orderRef":"A","note":null}""").RootElement)!.Keys.Order());
    }

    // ── localization and help (AC-23 pattern, AC-34; tenant module → 7 languages) ───────────────────────────────────────

    public static readonly TheoryData<string, string> Families = new()
    {
        { "OutboundShipments", "OutboundShipmentsIndex" },
        { "PickTasks", "PickTasksIndex" },
        { "Packing", "PackingIndex" },
        { "PutawayTasks", "PutawayTasksIndex" }
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void AC34_the_family_has_one_key_set_in_all_seven_languages_and_no_english_left_behind(string folder, string marker)
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
        { "OutboundShipments", "OutboundShipmentsIndex", "OutboundShipmentsController.cs" },
        { "PickTasks", "PickTasksIndex", "PickTasksController.cs" },
        { "Packing", "PackingIndex", "PackingController.cs" }
    };

    /// <summary>Every key a view, a controller or a script reads is in the family — the S1 error keys, the S2 error keys and the vocabulary.</summary>
    [Theory]
    [MemberData(nameof(Readers))]
    public void AC34_every_key_a_view_a_controller_or_the_script_reads_is_there(string folder, string marker, string controller)
    {
        var en = Family(folder, marker)["en"];
        var views = Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value));
        var source = File.ReadAllText(Path.Combine(Web, "Controllers", controller));
        var controllerKeys = Regex.Matches(source, @"""(Warehouse\.[\w.]+Saved\.\w+|Col\w+|Field\w+)""").Select(m => m.Groups[1].Value);
        var vocabulary = folder switch { "OutboundShipments" => WarehouseVocabulary.OutboundKeys, "PickTasks" => WarehouseVocabulary.PickKeys, _ => WarehouseVocabulary.PackingKeys };

        var missing = views.Concat(controllerKeys).Concat(vocabulary).Concat(WarehouseErrorMessages.AllKeys).Concat(WarehouseErrorMessages.S2Keys)
            .Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"{marker} lacks: {string.Join(", ", missing)}");
    }

    /// <summary>AC-34 (r24, son bakış 02 F-3; sabotages (cz), (eh)): the seven Warehouse.Origin.* values and the outbound vocabulary in all seven languages.</summary>
    [Fact]
    public void AC34_the_seven_origins_and_the_outbound_words_are_in_all_seven_languages()
    {
        foreach (var (folder, marker) in new[] { ("PutawayTasks", "PutawayTasksIndex"), ("PickTasks", "PickTasksIndex") })
        {
            var family = Family(folder, marker);
            foreach (var lang in Languages)
            foreach (var origin in WarehouseVocabulary.Origins)
                Assert.False(string.IsNullOrWhiteSpace(family[lang].GetValueOrDefault("Warehouse.Origin." + origin)), $"{marker}.{lang}: Warehouse.Origin.{origin}");
        }

        Assert.Equal(7, WarehouseVocabulary.Origins.Length);
        var outbound = Family("OutboundShipments", "OutboundShipmentsIndex");
        var words = outbound["en"].Keys.Where(k => k.StartsWith("Warehouse.Outbound.", StringComparison.Ordinal)).ToList();
        Assert.True(words.Count >= 40, $"only {words.Count} Warehouse.Outbound.* keys");
        foreach (var lang in Languages)
        foreach (var key in words)
            Assert.False(string.IsNullOrWhiteSpace(outbound[lang].GetValueOrDefault(key)), $"{lang}: {key}");
        // AC-33 (vi): the disabled "Sevke hazır" says why, in every language
        foreach (var lang in Languages)
            Assert.False(string.IsNullOrWhiteSpace(outbound[lang].GetValueOrDefault("Warehouse.Outbound.Disabled.ItemLevelNotPublishable")), lang);
    }

    [Fact]
    public void AC34_the_S2_nav_keys_are_in_all_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var shared = Rows(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            foreach (var key in new[] { "Nav.Page.OUTBOUNDSHIPMENTS", "Nav.Page.PICKTASKS", "Nav.Page.PACKINGSTATION", "Nav.Module.PUTAWAYPICKINGPACKING" })
                Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(key)), $"{lang}: {key}");
        }
    }

    public static readonly TheoryData<string, int, bool, string> HelpPages = new()
    {
        { "OUTBOUNDSHIPMENTS", 5, true, "Warehouse/OutboundShipments/Index.cshtml" },
        { "PICKTASKS", 3, false, "Warehouse/PickTasks/Index.cshtml" },
        { "PACKINGSTATION", 3, false, "Warehouse/Packing/Index.cshtml" }
    };

    /// <summary>UXH-001 (pack §24): purpose, audience, titled steps in all seven languages, Immediate only on the outbound page, related sentences, no tour.</summary>
    [Theory]
    [MemberData(nameof(HelpPages))]
    public void AC34_the_page_help_has_purpose_audience_and_titled_steps_in_all_seven_languages_and_no_tour(string code, int steps, bool immediate, string view)
    {
        foreach (var lang in Languages)
        {
            var help = Rows(Path.Combine(Web, "Resources", $"PageHelpResource.{lang}.resx"));
            var parts = new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, steps).SelectMany(n => new[] { $"Step{n}", $"Step{n}Title" }));
            foreach (var part in parts) Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.{part}")), $"{lang}: Help.{code}.{part}");
            Assert.False(help.ContainsKey($"Help.{code}.Step{steps + 1}"), $"{lang}: Help.{code} has more than {steps} steps");
            Assert.Equal(immediate, help.ContainsKey($"Help.{code}.Immediate"));
            Assert.All(help.Keys.Where(k => k.StartsWith($"Help.{code}.", StringComparison.Ordinal)),
                k => Assert.Matches($@"^Help\.{code}\.(Purpose|Audience|Immediate|Step[1-5](Title)?|Related\.[A-Z]+)$", k));
            foreach (var related in PageHelpCatalog.RelatedCodes.GetValueOrDefault(code) ?? [])
                Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.Related.{related}")), $"{lang}: Help.{code}.Related.{related}");
        }

        for (var n = 1; n <= steps; n++) Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, n));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, steps + 1));
        Assert.Contains($"ViewData[\"PageCode\"] = \"{code}\";", File.ReadAllText(Path.Combine([Web, "Views", .. view.Split('/')])), StringComparison.Ordinal);
    }

    // ── 375 px (AC-45, DL-3 CT F-7 / F-8), RTL (AC-50) ───────────────────────────────────────────────────────────

    /// <summary>
    /// AC-45 + DL-3 CT F-7: the close × of every panel / dialog of the warehouse screens (S1 confirm panel included) carries the 48 px touch class; the
    /// S2 panels have −/+ and a sticky bottom bar. F-8: the row menus of the lists are capped to the screen and their entries wrap.
    /// </summary>
    [Fact]
    public async Task AC45_F7_F8_every_close_button_is_a_48px_target_the_bars_stick_and_the_row_menu_stays_on_screen()
    {
        var pages = new[]
        {
            await RenderAsync("/Views/Warehouse/PutawayTasks/Index.cshtml", "en", "warehouse.putaway-tasks.read", "warehouse.putaway-tasks.confirm"),
            await RenderAsync(PickIndex, "en", [.. Operator, WarehousePermissionKeys.PickTasksUpdate, WarehousePermissionKeys.PickTasksResolve]),
            await RenderAsync(PackingIndex, "en", Operator),
            await RenderAsync(OutboundDetails, "en", OutboundAll)
        };
        // the module's own panels / dialogs (their × is labelled with the shared "Cancel"); the list shell's shared offcanvas ("Close") is the platform's
        var closes = pages.SelectMany(p => Regex.Matches(p, "<button[^>]*class=\"btn-close[^\"]*\"[^>]*aria-label=\"Cancel\"[^>]*>").Select(m => m.Value)).ToList();
        // 9 → 7 (WP-0178-E3-FIX-01): the Details page's "İptal" / "Sevke hazır" Bootstrap modals became window.showConfirm (MOD-0013), whose buttons
        // are the shared component's — the page draws two × fewer
        Assert.True(closes.Count >= 7, $"only {closes.Count} panel close buttons found");
        foreach (var close in closes)
            Assert.True(close.Contains("wh-touch", StringComparison.Ordinal), $"a close button without the 48 px class: {close}");
        Assert.Contains("id=\"offcanvasConfirm\"", pages[0], StringComparison.Ordinal);

        var pickPanel = Between(pages[1], "id=\"offcanvasConfirm\"", "id=\"modalPickReason\"");
        Assert.Equal(2, Regex.Matches(pickPanel, "<button[^>]*wh-touch[^>]*data-wh-step").Count);
        Assert.Contains("class=\"wh-action-bar", pickPanel, StringComparison.Ordinal);
        Assert.Matches("<button[^>]*wh-touch[^>]*id=\"btnConfirmSubmit\"", pickPanel);
        var packagePanel = Between(pages[2], "id=\"offcanvasPackage\"", "id=\"btnPackageSave\"");
        Assert.Contains("class=\"wh-action-bar", packagePanel, StringComparison.Ordinal);
        Assert.Matches("<input[^>]*id=\"packageWeight\"[^>]*wh-touch", packagePanel);
        Assert.Matches("id=\"d-actions\" class=\"wh-action-bar", pages[3]);

        var css = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "css", "Warehouse", "warehouse.css"));
        Assert.Matches(@"min-block-size:\s*48px", Regex.Match(css, @"\.wh-touch\s*\{([^}]*)\}").Groups[1].Value);
        Assert.Matches(@"max-inline-size:\s*calc\(100vw", Regex.Match(css, @"\.wh-list \.dropdown-menu\s*\{([^}]*)\}").Groups[1].Value);
        Assert.Matches(@"white-space:\s*normal", Regex.Match(css, @"\.wh-list \.dropdown-menu \.dropdown-item\s*\{([^}]*)\}").Groups[1].Value);
        foreach (var list in new[] { await RenderAsync(OutboundIndex, "en", WarehousePermissionKeys.OutboundShipmentsRead), pages[1] })
            Assert.Matches("<div class=\"wh-list\">\\s*(<!--.*?-->\\s*)?[\\s\\S]*?dt-", list);
        // the S2 JS steps the quantity with the same −/+ buttons (rules.js step)
        Assert.Contains("rules.step(", File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Warehouse", "Packing", "index.js")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    [InlineData("tr", "ltr")]
    [InlineData("fr", "ltr")]
    [InlineData("es", "ltr")]
    [InlineData("zh", "ltr")]
    [InlineData("ru", "ltr")]
    public async Task AC50_the_shell_turns_only_arabic_right_to_left_on_the_S2_pages(string culture, string direction)
    {
        foreach (var (view, keys) in new[] { (OutboundIndex, OutboundAll), (PickIndex, Operator), (PackingIndex, Operator) })
        {
            var html = await RenderAsync(view, culture, mainPage: true, keys);
            Assert.Matches($"<html lang=\"{culture}\"[^>]*dir=\"{direction}\"", html);
        }
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────

    private const string Key = "d5555555-0000-4000-8000-000000000005";

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

    private static PickTasksController Pick(FakeGateway gateway, string[] keys) =>
        new(new HttpClient(gateway), Configuration(), new Permissions(keys), new KeyLocalizer<PickTasksIndex>(), NullLogger<PickTasksController>.Instance) { ControllerContext = Context() };

    private static PackingController Packing(FakeGateway gateway, string[] keys) =>
        new(new HttpClient(gateway), Configuration(), new Permissions(keys), new KeyLocalizer<PackingIndex>(), NullLogger<PackingController>.Instance) { ControllerContext = Context() };

    private static OutboundShipmentsController Outbound(FakeGateway gateway, string[] keys) =>
        new(new HttpClient(gateway), Configuration(), new Permissions(keys), new KeyLocalizer<OutboundShipmentsIndex>(), NullLogger<OutboundShipmentsController>.Instance) { ControllerContext = Context() };

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

        public List<(string Method, string Path, string? IfMatch, string Body)> Requests { get; } = [];

        public void On(string prefix, int status, string body) => Routes.Add((prefix, status, body));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (path.StartsWith("/api/warehouse", StringComparison.Ordinal))
                lock (Requests) Requests.Add((request.Method.Method, path, request.Headers.TryGetValues("If-Match", out var m) ? m.Single() : null, body));
            var route = Routes.Where(r => path.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            return route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }

    private Task<string> RenderAsync(string viewPath, string culture, params string[] permissions) => RenderAsync(viewPath, culture, false, permissions);

    private async Task<string> RenderAsync(string viewPath, string culture, bool mainPage, params string[] permissions)
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
            httpContext.Request.Path = "/Warehouse/OutboundShipments";
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

            var engine = services.GetRequiredService<IRazorViewEngine>();
            var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: mainPage);
            if (!found.Success) throw new InvalidOperationException($"Could not load {viewPath}.");
            var actionContext = new ActionContext(httpContext, new RouteData { Values = { ["controller"] = "OutboundShipments", ["action"] = "Index" } },
                new ActionDescriptor(), new ModelStateDictionary());
            var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState)
            {
                Model = new OutboundShipmentFormViewModel { Id = OutboundId },
                ["OutboundId"] = OutboundId
            };
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
