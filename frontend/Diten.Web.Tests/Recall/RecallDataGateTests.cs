using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-0177-R6 (the FIX-WEBGATES pattern): every same-origin door of RecallController asks for ITS key. The doors are DISCOVERED from the
/// production controller (reflection: every routed action that does not return a page); <see cref="Gates"/> is the one table of their keys,
/// checked three ways — completeness (discovered = table, and there is NO catch-all proxy), the keys of each row = the keys of the door's own
/// first <c>if (!_permissions.Has(…)) return Refuse(403, …)</c> statement, and behaviour: without the key 403 and the FakeGateway sees
/// NOTHING, with the key the request reaches the gateway. R6 sabotage (e) removes one door's check.
/// </summary>
public sealed class RecallDataGateTests
{
    private const string Gateway = "http://gateway.test";
    private const string SampleId = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";

    /// <summary>THE gate table — door action → the key that opens it. Read from RecallController.cs.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Gates = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["DashboardData"] = [RecallKeys.DashboardRead],
        ["TraceData"] = [RecallKeys.TraceRun],
        ["ExportTrace"] = [RecallKeys.TraceExport],
        ["OpenCase"] = [RecallKeys.CasesCreate],
        ["LotLookup"] = [RecallKeys.TraceRun],
        ["ListCases"] = [RecallKeys.CasesRead],
        ["ProductLookup"] = [RecallKeys.CasesRead],
        // WP-0177-R7a — Vaka Detayı: one read door, one door per command (pack §11.3); each prepare door prepares ONLY its own decision
        ["CaseData"] = [RecallKeys.CasesRead],
        ["MarkScope"] = [RecallKeys.CasesUpdate],
        ["SubmitScope"] = [RecallKeys.CasesUpdate],
        ["ReturnScope"] = [RecallKeys.CasesApprove],
        ["PrepareApproval"] = [RecallKeys.CasesApprove],
        ["ApproveScope"] = [RecallKeys.CasesApprove],
        ["ReviseScope"] = [RecallKeys.CasesUpdate],
        ["RequestQuarantine"] = [RecallKeys.QuarantineRequestsCreate],
        ["RetryQuarantine"] = [RecallKeys.QuarantineRequestsCreate],
        ["ConfirmSegregation"] = [RecallKeys.SegregationsConfirm],
        ["RecordNotification"] = [RecallKeys.NotificationsCreate],
        ["DeclareConsumption"] = [RecallKeys.ConsumptionsCreate],
        ["LinkReturn"] = [RecallKeys.ReturnsLink],
        ["StartEffectiveness"] = [RecallKeys.EffectivenessComplete],
        ["PrepareCompletion"] = [RecallKeys.EffectivenessComplete],
        ["CompleteEffectiveness"] = [RecallKeys.EffectivenessComplete],
        ["PrepareClosure"] = [RecallKeys.CasesClose],
        ["CloseCase"] = [RecallKeys.CasesClose],
        ["CancelCase"] = [RecallKeys.CasesCancel],
        // WP-0177-R7b — Tatbikat Planlayıcı (read / manage / run), Kanıt Paketi (export), Ayarlar (read / manage)
        ["DrillPlans"] = [RecallKeys.DrillsRead],
        ["DrillRuns"] = [RecallKeys.DrillsRead],
        ["DrillRun"] = [RecallKeys.DrillsRead],
        ["DrillLotLookup"] = [RecallKeys.DrillsRun],
        ["CreateDrillPlan"] = [RecallKeys.DrillsManage],
        ["UpdateDrillPlan"] = [RecallKeys.DrillsManage],
        ["StartDrill"] = [RecallKeys.DrillsRun],
        ["CompleteDrillStep"] = [RecallKeys.DrillsRun],
        ["PrepareDrillResult"] = [RecallKeys.DrillsRun],
        ["SignDrillResult"] = [RecallKeys.DrillsRun],
        ["CancelDrill"] = [RecallKeys.DrillsRun],
        ["EvidenceSubjects"] = [RecallKeys.EvidenceExport],
        ["EvidencePreview"] = [RecallKeys.EvidenceExport],
        ["EvidenceExports"] = [RecallKeys.EvidenceExport],
        ["CreateEvidenceExport"] = [RecallKeys.EvidenceExport],
        ["EvidenceFile"] = [RecallKeys.EvidenceExport],
        ["SettingsData"] = [RecallKeys.SettingsRead],
        ["SettingsHistory"] = [RecallKeys.SettingsRead],
        ["PrepareSettings"] = [RecallKeys.SettingsManage],
        ["SaveSettings"] = [RecallKeys.SettingsManage],
        // WP-0177-R7c — the bands on 0173 / 0174 / 0175 (getLotRecallStatus, the one API-only key)
        ["LotRecallStatus"] = [RecallKeys.LotsStatusRead],
    };

    public static readonly string[] Pages = ["CaseDetails", "Cases", "Drills", "Evidence", "Index", "Settings", "Trace"];

    [Fact]
    public void Every_routed_action_is_a_page_or_a_door_of_the_gate_table_and_there_is_no_catch_all_proxy()
    {
        var actions = Routed().ToList();
        Assert.DoesNotContain(actions, a => a.Template.Contains("{**", StringComparison.Ordinal));
        var pages = actions.Where(a => a.Method.ReturnType == typeof(IActionResult)).Select(a => a.Method.Name).Distinct().Order(StringComparer.Ordinal).ToList();
        var doors = actions.Where(a => a.Method.ReturnType != typeof(IActionResult)).Select(a => a.Method.Name).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(Pages, pages);
        Assert.Equal(Gates.Keys.Order(StringComparer.Ordinal), doors);
        foreach (var page in Pages) Assert.Matches(@"=>\s*View\(", Body(Source(), page));
    }

    public static IEnumerable<object[]> Doors() => Gates.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Doors))]
    public void The_table_key_is_the_key_of_the_doors_own_403_statement(string door)
    {
        var body = Body(Source(), door);
        var gate = Regex.Match(body, @"if\s*\((?<cond>[^;]*?)\)\s*return\s+Refuse\(403", RegexOptions.Singleline);
        Assert.True(gate.Success, $"{door}: no `if (…) return Refuse(403, …)` statement");
        var keys = Regex.Matches(gate.Groups["cond"].Value, @"_permissions\.Has\(RecallKeys\.(?<k>\w+)\)")
            .Select(m => (string)typeof(RecallKeys).GetField(m.Groups["k"].Value)!.GetRawConstantValue()!).ToList();
        Assert.Equal(Gates[door], keys);
        var before = body[..gate.Index];
        Assert.DoesNotContain("SendAsync", before, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway(", before, StringComparison.Ordinal);
        Assert.DoesNotContain("TraceGatewayClient", before, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway(string door)
    {
        var keys = typeof(RecallKeys).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!).Except(Gates[door]).ToArray();
        var gateway = new FakeGateway();

        var result = await Invoke(door, gateway, keys);

        Assert.Equal(403, Status(result));
        Assert.True(gateway.Requests.Count == 0, $"{door} without {string.Join(" / ", Gates[door])} sent: {string.Join(", ", gateway.Requests)}");
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task With_its_key_a_door_reaches_the_gateway(string door)
    {
        var gateway = new FakeGateway();
        var result = await Invoke(door, gateway, Gates[door]);
        Assert.NotEqual(403, Status(result));
        Assert.True(gateway.Requests.Count > 0, $"{door} with {Gates[door][0]} sent nothing (status {Status(result)})");
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<(MethodInfo Method, string Template)> Routed()
    {
        foreach (var method in typeof(RecallController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in method.GetCustomAttributes<HttpMethodAttribute>()) yield return (method, attribute.Template ?? string.Empty);
        }
    }

    private static string Source()
        => File.ReadAllText(Path.Combine(Diten.Web.Tests.Traceability.TraceViewRenderer.WebRoot, "Controllers", "RecallController.cs"));

    private static string Body(string source, string method)
    {
        var signature = Regex.Match(source, $@"\n    public [^\n]*\b{method}\(");
        Assert.True(signature.Success, $"{method}: signature not found in RecallController.cs");
        var rest = source[(signature.Index + 1)..];
        var next = Regex.Match(rest[1..], @"\n    (\[|///|public |private |internal |protected )");
        return next.Success ? rest[..(next.Index + 1)] : rest;
    }

    /// <summary>The production controller over a fake gateway, a signed-in tenant user with <paramref name="keys"/> (R7a: shared with RecallCaseDetailWebTests).</summary>
    internal static RecallController Build(HttpMessageHandler gateway, string[] keys)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var claims = new List<Claim>
        {
            new("tenantId", "6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"), new("sub", "0d9c8b7a-6f5e-4d3c-2b1a-0f9e8d7c6b5a"),
            new("legal_entity_id", "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d")
        };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Headers.Cookie = "access_token=caller-token";
        return new RecallController(new Factory(gateway), new HttpClient(gateway), configuration, new KeySet(keys),
            new KeyLocalizer<Diten.Web.Views.Recall.Recall>(), NullLogger<RecallController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    internal static async Task<IActionResult> Invoke(string action, HttpMessageHandler gateway, string[] keys, string query = "", string? body = null)
    {
        var controller = Build(gateway, keys);
        var http = controller.ControllerContext.HttpContext;
        var method = typeof(RecallController).GetMethod(action, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        var post = method.GetCustomAttribute<HttpPostAttribute>() is not null;
        http.Request.Method = post ? "POST" : "GET";
        if (query.Length > 0) http.Request.QueryString = new QueryString(query);
        if (post)
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? "{\"idempotencyKey\":\"r6-key-0001\",\"rootLotId\":\"" + SampleId + "\",\"direction\":\"Both\",\"class\":2,\"level\":\"Pharmacy\",\"reason\":\"Tedarikci sapma bildirimi\"}");
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentLength = bytes.Length;
            http.Request.ContentType = "application/json";
        }

        var arguments = method.GetParameters().Select(p => Sample(action, p)).ToArray();
        return await (Task<IActionResult>)method.Invoke(controller, arguments)!;
    }

    private static object? Sample(string action, ParameterInfo parameter) => parameter.ParameterType == typeof(CancellationToken)
        ? CancellationToken.None
        : parameter.Name switch
        {
            "rootLotId" => SampleId,
            "direction" => "Both",
            "q" => "L-2026",
            "id" or "requestId" => SampleId,
            "step" => "Trace",
            "part" => null,
            _ => throw new InvalidOperationException($"{action}: no sample for parameter '{parameter.Name}' — add one here.")
        };

    internal static int Status(IActionResult result) => result switch
    {
        ContentResult content => content.StatusCode ?? 200,
        ObjectResult obj => obj.StatusCode ?? 200,
        JsonResult json => json.StatusCode ?? 200,
        StatusCodeResult code => code.StatusCode,
        FileContentResult => 200,
        _ => 200
    };

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class KeySet(string[] keys) : IPermissionSnapshot
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

    /// <summary>Records every request (path + query) and answers 200 with a small body of the right shape for the door.</summary>
    internal sealed class FakeGateway : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage>? Answer { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.RequestUri!.PathAndQuery);
            if (Answer is not null) return Task.FromResult(Answer(request));
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/trace/export", StringComparison.Ordinal) ? "﻿\"Düğüm türü\"\r\n" : "{}";
            var type = path.EndsWith("/trace/export", StringComparison.Ordinal) ? "text/csv" : "application/json";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, type) });
        }
    }
}
