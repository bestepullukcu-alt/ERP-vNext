using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Tests.Traceability;
using Diten.Web.Views.Traceability.Lots;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Expiry;

/// <summary>
/// WP-0176-F5 — the Web side of the exception inbox / detail / drawer, settings, evidence pack and the three bands (pack §11.1–§11.3, §8.3,
/// §16 F5), measured on the real Razor render, the shipped scripts (Node, <see cref="ExpiryF5NodeTests"/>) and the seams of CT decision A:
/// <list type="bullet">
/// <item>UAS-001 for the four new pages; "Karar ver" (and the drawer, the MOD-0022 dialog) only with expiry.exceptions.resolve (K3 (a));</item>
/// <item>the movement payload without a reason is byte-for-byte the pre-F5 one (K3 (i)); the refusal details are whitelisted (K3 (j));
/// createLot carries the signed dateOverride only when sent (K3 (k));</item>
/// <item>CT-F12: /InventoryGovernance answers 404 — no controller, no route, no menu entry, no redirect (K3 (f)).</item>
/// </list>
/// </summary>
public sealed class ExpiryF5WebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Company = Guid.Parse("77777777-0000-0000-0000-000000000001");

    private readonly WebApplicationFactory<Program> _factory;

    public ExpiryF5WebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public static readonly TheoryData<string, string, string> Pages = new()
    {
        { "Exceptions", "expiry.exceptions.read", "dt-expiry-exceptions" },
        { "ExceptionDetails", "expiry.exceptions.read", "ex-d-body" },
        { "Policy", "expiry.policies.read", "ex-policy-form" },
        { "Evidence", "expiry.evidence.export", "ex-ev-form" }
    };

    private Task<string> Render(string view, params string[] keys)
        => TraceViewRenderer.RenderAsync(_factory, $"/Views/Expiry/{view}.cshtml", view == "ExceptionDetails" ? "11111111-0000-4000-8000-000000000001" : null, keys);

    // ── UAS-001 + buttons by key ─────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task UAS_001_the_new_pages_draw_only_the_explanation_without_their_key(string view, string pageKey, string skeleton)
    {
        var others = new[] { "expiry.dashboard.read", "expiry.exceptions.read", "expiry.policies.read", "expiry.evidence.export", "expiry.exceptions.resolve" }
            .Where(k => k != pageKey).ToArray();
        var denied = await Render(view, others);
        Assert.Contains("diten-access-denied", denied, StringComparison.Ordinal);
        Assert.DoesNotContain(skeleton, denied, StringComparison.Ordinal);
        Assert.DoesNotContain("ex-tabs", denied, StringComparison.Ordinal);

        var allowed = await Render(view, pageKey);
        Assert.DoesNotContain("diten-access-denied", allowed, StringComparison.Ordinal);
        Assert.Contains(skeleton, allowed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task K3_a_Karar_ver_the_drawer_and_the_signature_dialog_exist_only_with_the_resolve_key()
    {
        var reader = await Render("ExceptionDetails", "expiry.exceptions.read");
        foreach (var id in new[] { "ex-d-decide", "ex-drawer", "esignDialog", "ex-d-take", "ex-d-close", "ex-d-comment" })
            Assert.DoesNotContain($"id=\"{id}\"", reader, StringComparison.Ordinal);
        Assert.Contains("id=\"ex-d-no-decide\"", reader, StringComparison.Ordinal);

        var qa = await Render("ExceptionDetails", "expiry.exceptions.read", "expiry.exceptions.resolve", "expiry.exceptions.review",
            "expiry.exceptions.close", "expiry.exceptions.comment");
        foreach (var id in new[] { "ex-d-decide", "ex-drawer", "esignDialog", "ex-d-take", "ex-d-close", "ex-d-comment" })
            Assert.Contains($"id=\"{id}\"", qa, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ex-d-no-decide\"", qa, StringComparison.Ordinal);

        // the drawer asks no password: the ONE MOD-0022 dialog signs (window.ESign.open), its own markup is _ESignatureDialog's
        var drawer = File.ReadAllText(Path.Combine(Web, "Views", "Expiry", "_DecisionDrawer.cshtml"));
        Assert.DoesNotContain("type=\"password\"", drawer, StringComparison.Ordinal);
        var detail = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Expiry", "exception-detail.js"));
        Assert.Contains("window.ESign.open(subject)", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("password", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_other_buttons_follow_their_keys_and_the_settings_are_read_only_without_manage()
    {
        var inbox = await Render("Exceptions", "expiry.exceptions.read");
        Assert.DoesNotContain("id=\"ex-inbox-open\"", inbox, StringComparison.Ordinal);
        Assert.Contains("id=\"dt-expiry-exceptions\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", inbox, StringComparison.Ordinal);
        Assert.Contains("id=\"ex-inbox-open\"", await Render("Exceptions", "expiry.exceptions.read", "expiry.exceptions.create"), StringComparison.Ordinal);

        var reader = await Render("Policy", "expiry.policies.read");
        Assert.Contains("id=\"ex-policy-readonly\"", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ex-p-save\"", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ex-p-modal\"", reader, StringComparison.Ordinal);
        Assert.Matches(new Regex("id=\"ex-p-approaching\"[^>]*disabled"), reader);
        var admin = await Render("Policy", "expiry.policies.read", "expiry.policies.manage");
        Assert.Contains("id=\"ex-p-save\"", admin, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ex-policy-readonly\"", admin, StringComparison.Ordinal);

        var evidence = await Render("Evidence", "expiry.evidence.export");
        foreach (var id in new[] { "ex-ev-pdf", "ex-ev-csv", "ex-ev-log" }) Assert.Contains($"id=\"{id}\"", evidence, StringComparison.Ordinal);
        // the log shows the hash shortened, the whole value copyable (mockup "logs")
        var script = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Expiry", "evidence.js"));
        Assert.Contains("`sha256:${sha.slice(0, 6)}…${sha.slice(-4)}`", script, StringComparison.Ordinal);
        Assert.Contains("data-ex-copy=", script, StringComparison.Ordinal);
        Assert.Contains("/Expiry/evidence/file/", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_new_tabs_are_offered_by_their_keys_and_O_F4_4_links_the_expired_badge_to_the_case()
    {
        var header = File.ReadAllText(Path.Combine(Web, "Views", "Expiry", "_Header.cshtml"));
        foreach (var (route, key) in new[] { ("/Expiry/Exceptions", "expiry.exceptions.read"), ("/Expiry/Policy", "expiry.policies.read"), ("/Expiry/Evidence", "expiry.evidence.export") })
            Assert.Matches(new Regex($"\"{Regex.Escape(route)}\"[^\\n]*\"{Regex.Escape(key)}\""), header);
        var expired = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Expiry", "expired.js"));
        Assert.Contains("href=\"/Expiry/Exceptions/Details/${encodeURIComponent(data)}\"", expired, StringComparison.Ordinal);
    }

    // ── the bands on MOD-0173 / MOD-0174 ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_bands_sit_on_the_movement_and_lot_forms_and_carry_their_assets_once()
    {
        var movement = File.ReadAllText(Path.Combine(Web, "Views", "Inventory", "StockMovements", "_Form.cshtml"));
        Assert.Contains("~/Views/Shared/Expiry/_FefoBand.cshtml", movement, StringComparison.Ordinal);
        Assert.Contains("~/Views/Shared/Expiry/_MinRemainingBand.cshtml", movement, StringComparison.Ordinal);
        Assert.Contains("~/Views/Shared/Expiry/_ExpiryBandAssets.cshtml", movement, StringComparison.Ordinal);
        var lot = File.ReadAllText(Path.Combine(Web, "Views", "Traceability", "Lots", "_Form.cshtml"));
        Assert.Contains("_DateOverrideBand.cshtml\" model=\"@(\"lot\")\"", lot, StringComparison.Ordinal);
        var quick = File.ReadAllText(Path.Combine(Web, "Views", "Traceability", "Lots", "_LotQuickCreateModal.cshtml"));
        Assert.Contains("_DateOverrideBand.cshtml\" model=\"@(\"quick\")\"", quick, StringComparison.Ordinal);

        var form = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Inventory", "StockMovements", "form.js"));
        Assert.Contains("window.ExpiryBands?.movement.onSource(", form, StringComparison.Ordinal);
        Assert.Contains("window.ExpiryBands?.movement.refused(res?.status, json, body)", form, StringComparison.Ordinal);
        Assert.Contains("post({ ...body, idempotencyKey: key, toStockStatus: band.toStockStatus });", form, StringComparison.Ordinal);
        Assert.Contains("window.ExpiryBands.lot.beforeSave('lot', b.lotNumber)", File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Traceability", "Lots", "form.js")), StringComparison.Ordinal);
        Assert.Contains("window.ExpiryBands.lot.beforeSave('quick', lot.lotNumber)", File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Traceability", "Lots", "quick-create.js")), StringComparison.Ordinal);
    }

    [Fact]
    public void K3_i_a_movement_without_a_reason_posts_the_pre_F5_payload_byte_for_byte()
    {
        var form = Movement();
        var payload = Json(MovementRules.Check(form, MovementRules.ManualTypes, "mov-key").Payload!);
        // written out by hand: the pre-F5 body of this form (the order of the members is the payload builder's)
        const string pre = "{\"idempotencyKey\":\"mov-key\",\"movementType\":\"SCRAP\",\"itemId\":\"aaaaaaaa-0000-4000-8000-000000000001\","
            + "\"skuId\":\"bbbbbbbb-0000-4000-8000-000000000002\",\"skuLevel\":\"Lsku\",\"quantity\":\"7.5\",\"uomId\":\"ADET\",\"sourceModule\":\"MOD-0173\","
            + "\"sourceType\":\"MANUAL_MOVEMENT\",\"transactionDate\":\"2026-10-13T08:00:00.0000000Z\",\"postingDate\":\"2026-10-13T08:00:00.0000000Z\","
            + "\"fromWarehouseId\":\"cccccccc-0000-4000-8000-000000000003\",\"fromLocationId\":\"dddddddd-0000-4000-8000-000000000004\","
            + "\"fromStockStatus\":\"AVAILABLE\",\"lotId\":\"eeeeeeee-0000-4000-8000-000000000005\",\"sourceDocumentId\":\"SAY-1\"}";
        Assert.Equal(Normalize(pre, payload), payload);
        Assert.DoesNotContain("fefoBypassReason", payload, StringComparison.Ordinal);
        Assert.Equal(payload, Json(MovementRules.Check(Movement("   "), MovementRules.ManualTypes, "mov-key").Payload!)); // blank = absent

        var reasoned = Json(MovementRules.Check(Movement("  Customer asked for the longer-dated lot  "), MovementRules.ManualTypes, "mov-key").Payload!);
        Assert.Equal(payload[..^1] + ",\"fefoBypassReason\":\"Customer asked for the longer-dated lot\"}", reasoned);
    }

    [Fact]
    public void K3_j_only_the_three_expiry_codes_carry_details_and_only_the_whitelisted_keys()
    {
        var details = new Dictionary<string, object?>
        {
            ["firstEligibleLotId"] = "lot-a", ["lotNumber"] = "A-1", ["expiryDate"] = "2027-01-01", ["requiredToStockStatus"] = "QUARANTINE",
            ["remainingDays"] = 40, ["requiredDays"] = 180, ["internalNote"] = "never to the browser", ["candidateCount"] = 3
        };
        foreach (var code in new[] { "FEFO_BYPASS_REASON_REQUIRED", "FEFO_ORDER_VIOLATION", "LOT_BELOW_MIN_REMAINING_SHELF_LIFE" })
        {
            var kept = StockMovementsController.ExpiryRefusalDetails(code, details)!;
            Assert.Equal(["firstEligibleLotId", "lotNumber", "expiryDate", "requiredToStockStatus", "remainingDays", "requiredDays"], kept.Keys);
        }

        Assert.Null(StockMovementsController.ExpiryRefusalDetails("INSUFFICIENT_STOCK", details));
        Assert.Null(StockMovementsController.ExpiryRefusalDetails(null, details));
        Assert.Empty(StockMovementsController.ExpiryRefusalDetails("LOT_BELOW_MIN_REMAINING_SHELF_LIFE", new Dictionary<string, object?> { ["remainingDays"] = null })!);
        var controller = File.ReadAllText(Path.Combine(Web, "Controllers", "StockMovementsController.cs"));
        Assert.Contains("var expiry = ExpiryRefusalDetails(code, details);", controller, StringComparison.Ordinal);
    }

    [Fact]
    public async Task K3_k_a_signed_date_override_reaches_createLot_and_an_absent_one_leaves_the_body_unchanged()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/lots", 201, "{\"lotId\":\"99999999-0000-4000-8000-000000000009\",\"lotNumber\":\"L-26\",\"status\":\"Draft\"}");
        var overrideId = "12121212-0000-4000-8000-000000000012";
        var form = new LotFormInput
        {
            IdempotencyKey = Guid.NewGuid().ToString(), ItemId = "aaaaaaaa-0000-4000-8000-000000000001", Sku = "Lsku:bbbbbbbb-0000-4000-8000-000000000002",
            LotNumber = "L-26", ManufacturingDate = "2026-10-01", ExpiryDate = "2028-12-31", Origin = "lot-form",
            DateOverride = new LotDateOverrideInput { OverrideId = overrideId, SignatureId = "  sig-777  " }
        };
        var signed = await Lots(gateway).CreateLot(form, CancellationToken.None);
        Assert.IsType<JsonResult>(signed);
        var body = JsonDocument.Parse(Assert.Single(gateway.Requests).Body).RootElement;
        Assert.Equal(overrideId, body.GetProperty("dateOverride").GetProperty("overrideId").GetString());
        Assert.Equal("sig-777", body.GetProperty("dateOverride").GetProperty("signatureId").GetString());

        gateway.Requests.Clear();
        await Lots(gateway).CreateLot(form with { DateOverride = null }, CancellationToken.None);
        Assert.False(JsonDocument.Parse(Assert.Single(gateway.Requests).Body).RootElement.TryGetProperty("dateOverride", out _));
        gateway.Requests.Clear();
        await Lots(gateway).CreateLot(form with { DateOverride = new LotDateOverrideInput { OverrideId = "not-a-uuid" } }, CancellationToken.None);
        Assert.False(JsonDocument.Parse(Assert.Single(gateway.Requests).Body).RootElement.TryGetProperty("dateOverride", out _));

        // a refusal keeps its LOT_DATE_OVERRIDE_* code for the band
        var refusing = new FakeGateway();
        refusing.On("/api/trace/lots", 409, "{\"error\":{\"code\":\"LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH\",\"message\":\"x\"}}");
        var refused = Assert.IsType<JsonResult>(await Lots(refusing).CreateLot(form, CancellationToken.None));
        Assert.Equal(409, refused.StatusCode);
        Assert.Contains("LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH", JsonSerializer.Serialize(refused.Value), StringComparison.Ordinal);
    }

    // ── CT-F12 ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task K3_f_InventoryGovernance_is_gone_and_its_url_is_404_without_a_redirect()
    {
        Assert.Null(typeof(ExpiryController).Assembly.GetType("Diten.Web.Controllers.InventoryGovernanceController"));
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        Assert.DoesNotContain(endpoints, e => e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerName == "InventoryGovernance");

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var url in new[] { "/InventoryGovernance", "/InventoryGovernance/Index" })
        {
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url}: {(int)response.StatusCode} {response.Headers.Location}");
        }

        Assert.DoesNotContain("InventoryGovernance", File.ReadAllText(Path.Combine(Web, "Views", "Shared", "_Layout.cshtml")), StringComparison.Ordinal);
        var registry = File.ReadAllText(Path.Combine(Web, "Config", "ManagementGovernanceRegistry.cs"));
        Assert.DoesNotContain("/InventoryGovernance", registry, StringComparison.Ordinal);
        Assert.Contains("\"Risk Office\", \"High\", \"/Expiry\", true)", registry, StringComparison.Ordinal);
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static MovementSubmitViewModel Movement(string? reason = null) => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString(), MovementType = "SCRAP", ItemId = "aaaaaaaa-0000-4000-8000-000000000001",
        Sku = "Lsku:bbbbbbbb-0000-4000-8000-000000000002",
        Source = "cccccccc-0000-4000-8000-000000000003|dddddddd-0000-4000-8000-000000000004|AVAILABLE|eeeeeeee-0000-4000-8000-000000000005",
        Quantity = "7.5", UomId = "ADET", DocumentNo = "SAY-1", TransactionDate = "2026-10-13T08:00:00Z", PostingDate = "2026-10-13T08:00:00Z",
        FefoBypassReason = reason
    };

    private static string Json(IReadOnlyDictionary<string, object?> payload) => JsonSerializer.Serialize(payload);

    /// <summary>The dates are the builder's own UTC rendering; the hand-written body takes them from the measured one (only those two).</summary>
    private static string Normalize(string expected, string measured)
    {
        var date = Regex.Match(measured, "\"transactionDate\":(\"[^\"]*\")").Groups[1].Value;
        var posting = Regex.Match(measured, "\"postingDate\":(\"[^\"]*\")").Groups[1].Value;
        return expected.Replace("\"transactionDate\":\"2026-10-13T08:00:00.0000000Z\"", $"\"transactionDate\":{date}", StringComparison.Ordinal)
            .Replace("\"postingDate\":\"2026-10-13T08:00:00.0000000Z\"", $"\"postingDate\":{posting}", StringComparison.Ordinal);
    }

    private static LotsController Lots(FakeGateway gateway)
    {
        var controller = new LotsController(new HttpClient(gateway),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build(),
            new Permissions([TracePermissionKeys.LotsCreate, TracePermissionKeys.LotsRead]), new KeyLocalizer<LotsIndex>(), new KeyLocalizer<LotFieldIndex>(),
            NullLogger<LotsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "55555555-0000-0000-0000-000000000001") };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Headers.Cookie = $"{CompanySelection.CookieName(http.User)}={Company}";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

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

    private sealed class FakeGateway : HttpMessageHandler
    {
        private readonly List<(string Prefix, int Status, string Body)> _routes = [];

        public List<(string Method, string Path, string Body)> Requests { get; } = [];

        public void On(string prefix, int status, string body) { Contracts.ContractRoutes.Require(prefix, status, body); _routes.Add((prefix, status, body)); } // FU-WEB-CONTRACT-FAKES

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests) Requests.Add((request.Method.Method, path, body));
            var route = _routes.FirstOrDefault(r => path.StartsWith(r.Prefix, StringComparison.Ordinal));
            return route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }
}

/// <summary>
/// The bridge that makes `dotnet test` run the F5 Node tests against the SHIPPED expiry-decision.js / expiry-bands.js (the Q382 pattern:
/// a missing `node` fails here instead of skipping): the drawer end to end (prepare → the MOD-0022 signature test double → resolve →
/// close), the skipped signature only on signatureRequired: false (K3 (e)), the CT-F1 resubmission into QUARANTINE (K3 (c)) and no
/// resubmission under "Reddet" (K3 (d)).
/// </summary>
public sealed class ExpiryF5NodeTests
{
    [Fact]
    public async Task The_drawer_and_the_bands_hold_against_the_shipped_scripts()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "expiry-f5.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 8$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
