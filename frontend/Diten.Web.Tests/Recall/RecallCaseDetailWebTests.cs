using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Diten.Web.Tests.Recall.RecallDataGateTests;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-0177-R7a — Vaka Detayı (RECALLCASEDETAILS) as the server draws it and as its doors forward:
/// <list type="bullet">
/// <item>UAS-001: without recall.cases.read only the access explanation — no skeleton, no redirect;</item>
/// <item>every §11.3 button / form is DRAWN only for the holder of its key — absent (not disabled) otherwise (R7a sabotage (a));</item>
/// <item>D-26 / D-27: no target date, no "Müşteri iadesi gir" button, and no Recall door or script posts a stock movement (R7a sabotage (d));</item>
/// <item>the command doors forward the body, the body's Idempotency-Key and the page's If-Match; a prepare door names ITS decision whatever the body
/// says; the read door takes only its closed list of parts.</item>
/// </list>
/// </summary>
public sealed class RecallCaseDetailWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string View = "/Views/Recall/CaseDetails.cshtml";
    private const string CaseId = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";
    private static readonly string Web = TraceViewRenderer.WebRoot;

    private static readonly string[] CaseKeys =
    [
        RecallKeys.CasesRead, RecallKeys.CasesUpdate, RecallKeys.CasesApprove, RecallKeys.CasesCancel, RecallKeys.CasesClose, RecallKeys.EffectivenessComplete,
        RecallKeys.QuarantineRequestsCreate, RecallKeys.SegregationsConfirm, RecallKeys.NotificationsCreate, RecallKeys.ConsumptionsCreate, RecallKeys.ReturnsLink
    ];

    /// <summary>Server-drawn control (its markup) → the key it needs (pack §11.3).</summary>
    public static readonly TheoryData<string, string> Controls = new()
    {
        { "data-rc-action=\"approve\"", RecallKeys.CasesApprove },
        { "data-rc-action=\"return\"", RecallKeys.CasesApprove },
        { "data-rc-action=\"submit\"", RecallKeys.CasesUpdate },
        { "data-rc-action=\"save-marks\"", RecallKeys.CasesUpdate },
        { "data-rc-action=\"revise\"", RecallKeys.CasesUpdate },
        { "data-rc-action=\"quarantine-send\"", RecallKeys.QuarantineRequestsCreate },
        { "data-rc-action=\"eff-start\"", RecallKeys.EffectivenessComplete },
        { "data-rc-action=\"eff-complete\"", RecallKeys.EffectivenessComplete },
        { "data-rc-action=\"close\"", RecallKeys.CasesClose },
        { "data-rc-action=\"cancel\"", RecallKeys.CasesCancel },
        { "id=\"rc-notify-modal\"", RecallKeys.NotificationsCreate },
        { "id=\"rc-consume-modal\"", RecallKeys.ConsumptionsCreate },
        { "id=\"rc-link-modal\"", RecallKeys.ReturnsLink },
        { "id=\"rc-seg-modal\"", RecallKeys.SegregationsConfirm }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public RecallCaseDetailWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    [Fact]
    public async Task UAS_001_without_recall_cases_read_only_the_access_explanation_is_drawn()
    {
        var denied = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, CaseKeys.Where(k => k != RecallKeys.CasesRead).ToArray());
        Assert.Contains("diten-access-denied", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"rc-case\"", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("data-rc-action", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("rc-tabs", denied, StringComparison.Ordinal);

        var allowed = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, RecallKeys.CasesRead);
        Assert.DoesNotContain("diten-access-denied", allowed, StringComparison.Ordinal);
        Assert.Contains($"data-case-id=\"{CaseId}\"", allowed, StringComparison.Ordinal);
        Assert.Contains("id=\"rc-case-tabs\"", allowed, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task A_button_is_drawn_only_for_the_holder_of_its_key(string control, string key)
    {
        var without = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, CaseKeys.Where(k => k != key).ToArray());
        Assert.DoesNotContain(control, without, StringComparison.Ordinal);
        var with = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, CaseKeys);
        Assert.Contains(control, with, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_signature_dialog_is_on_the_page_only_for_a_user_who_can_sign_a_decision()
    {
        var reader = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, RecallKeys.CasesRead, RecallKeys.NotificationsCreate);
        Assert.DoesNotContain("esign-dialog.js", reader, StringComparison.Ordinal);
        Assert.Contains("\"RECALL_SCOPE_APPROVE\":false", reader.Replace(" ", ""), StringComparison.Ordinal);
        foreach (var key in new[] { RecallKeys.CasesApprove, RecallKeys.EffectivenessComplete, RecallKeys.CasesClose })
        {
            var signer = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, RecallKeys.CasesRead, key);
            Assert.Contains("esign-dialog.js", signer, StringComparison.Ordinal);
            Assert.Contains("id=\"rc-sign-modal\"", signer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task No_target_date_no_customer_return_entry_and_the_returns_tab_says_where_a_return_comes_from()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, View, CaseId, CaseKeys);
        foreach (var absent in new[] { "Target date", "Hedef tarih", "Customer return entry", "Müşteri iadesi gir", "/StockMovements", "toStatus=QUARANTINE" })
            Assert.DoesNotContain(absent, html, StringComparison.Ordinal); // D-26, D-27
        Assert.Contains("id=\"rc-ret-info\"", html, StringComparison.Ordinal);
        Assert.Contains("A customer return is recorded as a stock movement; once recorded, it appears here as a candidate.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo", html, StringComparison.Ordinal); // D-14: no status selector, no role switcher
    }

    [Fact]
    public void No_recall_door_or_script_posts_a_stock_movement()
    {
        // D-3 / D-27 (R7a sabotage (d)): every gateway path of the Recall doors is a RECALL operation or the TRACE lot search — never a movement
        var controller = File.ReadAllText(Path.Combine(Web, "Controllers", "RecallController.cs"));
        var targets = Regex.Matches(controller, @"SendAsync\(HttpMethod\.\w+,\s*\$?""(?<p>[^""]*)""").Select(m => m.Groups["p"].Value).ToList();
        Assert.NotEmpty(targets);
        Assert.All(targets, p => Assert.StartsWith("{ApiBase}", p, StringComparison.Ordinal));
        Assert.Matches(new Regex(@"public const string ApiBase = ""/api/recall"";"), controller);
        Assert.DoesNotContain("StockMovement", Code(controller), StringComparison.Ordinal);
        Assert.DoesNotContain("/api/inventory", Code(controller), StringComparison.Ordinal);

        var scripts = Directory.EnumerateFiles(Path.Combine(Web, "wwwroot", "assets", "js", "Recall"), "*.js").ToList();
        Assert.Contains(scripts, f => f.EndsWith("case-detail.js", StringComparison.Ordinal));
        foreach (var file in scripts.Concat(Directory.EnumerateFiles(Path.Combine(Web, "Views", "Recall"), "*.cshtml")))
        {
            // R7c (D-27 closed): the ONE allowed mention is the GET link that OPENS the MOD-0173 form ("Müşteri iadesi gir" — case-view.js builds it,
            // CaseDetails.cshtml draws it); MOD-0173 posts the movement. Taken out here, every other mention still fails.
            var code = Regex.Replace(Code(File.ReadAllText(file)), @"(""|`)/StockMovements/Create\?(type=CUSTOMER_RETURN|\$\{query\})[^""`]*(""|`)|new URLSearchParams\(\{ type: 'CUSTOMER_RETURN' \}\)", string.Empty);
            foreach (var forbidden in new[] { "StockMovements", "/api/inventory", "/movements", "CUSTOMER_RETURN" })
                Assert.False(code.Contains(forbidden, StringComparison.Ordinal), $"{Path.GetFileName(file)} names {forbidden}");
        }
    }

    // ── what the doors forward ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_command_door_forwards_the_body_its_idempotency_key_and_the_pages_If_Match()
    {
        var gateway = new Recorder();
        var result = await InvokeWithHeaders("SubmitScope", gateway, [RecallKeys.CasesUpdate], "{\"idempotencyKey\":\"r7-key-0001\",\"note\":\"Kapsam hazir\"}", "W/\"7\"");
        Assert.Equal(200, Status(result));
        var sent = Assert.Single(gateway.Sent);
        Assert.Equal(($"/api/recall/cases/{CaseId}/scope/submit", "POST", "r7-key-0001", "W/\"7\""), (sent.Path, sent.Method, sent.Key, sent.IfMatch));
        Assert.Equal("Kapsam hazir", JsonDocument.Parse(sent.Body!).RootElement.GetProperty("note").GetString());
    }

    [Theory]
    [InlineData("PrepareApproval", "ApproveScope")]
    [InlineData("PrepareCompletion", "CompleteEffectiveness")]
    [InlineData("PrepareClosure", "Close")]
    public async Task A_prepare_door_prepares_only_its_own_decision_whatever_the_body_says(string door, string action)
    {
        var gateway = new Recorder();
        await InvokeWithHeaders(door, gateway, Gates[door], "{\"idempotencyKey\":\"r7-key-0002\",\"action\":\"Close\",\"summary\":\"Kapanis ozeti burada\"}", "W/\"3\"");
        var sent = Assert.Single(gateway.Sent);
        Assert.Equal($"/api/recall/cases/{CaseId}/pending-action", sent.Path);
        Assert.Equal(action, JsonDocument.Parse(sent.Body!).RootElement.GetProperty("action").GetString());
        Assert.Equal("W/\"3\"", sent.IfMatch);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("scope", "/scope")]
    [InlineData("diff", "/scope/diff")]
    [InlineData("stock", "/stock")]
    [InlineData("customers", "/customers")]
    [InlineData("returns", "/returns")]
    [InlineData("effectiveness", "/effectiveness")]
    public async Task The_read_door_reads_only_its_parts(string part, string suffix)
    {
        var gateway = new Recorder();
        var result = await InvokeRead(gateway, part);
        Assert.Equal(200, Status(result));
        Assert.Equal($"/api/recall/cases/{CaseId}{suffix}", Assert.Single(gateway.Sent).Path);
    }

    [Theory]
    [InlineData("../settings")]
    [InlineData("timeline/../../drill-runs")]
    [InlineData("evidence")]
    public async Task An_unknown_part_never_reaches_the_gateway(string part)
    {
        var gateway = new Recorder();
        Assert.Equal(400, Status(await InvokeRead(gateway, part)));
        Assert.Empty(gateway.Sent);
    }

    private static async Task<IActionResult> InvokeRead(Recorder gateway, string part)
    {
        var controller = Controller(gateway, [RecallKeys.CasesRead], "GET", null, null);
        return await controller.CaseData(CaseId, part.Length == 0 ? null : part, CancellationToken.None);
    }

    private static async Task<IActionResult> InvokeWithHeaders(string door, Recorder gateway, string[] keys, string body, string ifMatch)
    {
        var controller = Controller(gateway, keys, "POST", body, ifMatch);
        var method = typeof(RecallController).GetMethod(door)!;
        return await (Task<IActionResult>)method.Invoke(controller, [CaseId, CancellationToken.None])!;
    }

    private static RecallController Controller(Recorder gateway, string[] keys, string method, string? body, string? ifMatch)
    {
        // the gate test's plumbing builds the controller; here the request carries a body and an If-Match of our own
        var controller = Build(gateway, keys);
        var request = controller.ControllerContext.HttpContext.Request;
        request.Method = method;
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            request.Body = new MemoryStream(bytes);
            request.ContentLength = bytes.Length;
            request.ContentType = "application/json";
        }

        if (ifMatch is not null) request.Headers.IfMatch = ifMatch;
        return controller;
    }

    /// <summary>Records path, method, Idempotency-Key, If-Match and body of every request; answers 200 with the contract-shaped placeholder.</summary>
    internal sealed class Recorder : HttpMessageHandler
    {
        public List<(string Path, string Method, string? Key, string? IfMatch, string? Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Sent)
            {
                Sent.Add((request.RequestUri!.AbsolutePath, request.Method.Method,
                    request.Headers.TryGetValues("Idempotency-Key", out var k) ? k.Single() : null,
                    request.Headers.TryGetValues("If-Match", out var m) ? string.Join(",", m) : null, body));
            }

            // WP-MVP1-FU-CLEAN2 (FU-WEB-CONTRACT-FAKES-RECALL): the placeholder is bound to RECALL 1.9.0 (it may only miss required members)
            return RecallContractFakes.Placeholder(request);
        }
    }
}
