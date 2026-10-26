using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Diten.Web.Tests.Recall.RecallDataGateTests;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-0177-R7b — Tatbikat Planlayıcı, Kanıt Paketi, Ayarlar as the server draws them and as their doors forward:
/// <list type="bullet">
/// <item>every §11.3 control is DRAWN only for the holder of its key — absent, not disabled (R7b sabotage (e): "Plan ekle" without recall.drills.manage);</item>
/// <item>the links between the pages: the case detail's "Kanıt paketi" (recall.evidence.export), the returns' quarantine record column (D-28);</item>
/// <item>the drill doors add the SERVICE's clock (its Date header) — the page's clock never comes from the browser (sabotage (c) is measured by Node);</item>
/// <item>the command doors forward body, Idempotency-Key and If-Match; the settings save is a PUT, the file door returns the service's bytes unchanged;</item>
/// <item>every word the evidence preview can send is in the seven languages (RecallEvidenceLabels ↔ Recall.Ev.W.*).</item>
/// </list>
/// </summary>
public sealed class RecallR7bWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RunId = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public RecallR7bWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    /// <summary>(view, server-drawn control, the key it needs, the page key) — pack §11.3.</summary>
    public static readonly TheoryData<string, string, string, string> Controls = new()
    {
        { "Drills", "id=\"rc-plan-add\"", RecallKeys.DrillsManage, RecallKeys.DrillsRead },
        { "Drills", "id=\"rc-plan-modal\"", RecallKeys.DrillsManage, RecallKeys.DrillsRead },
        { "Drills", "id=\"rc-drill-step\"", RecallKeys.DrillsRun, RecallKeys.DrillsRead },
        { "Drills", "id=\"rc-drill-cancel\"", RecallKeys.DrillsRun, RecallKeys.DrillsRead },
        { "Drills", "id=\"rc-drill-sign\"", RecallKeys.DrillsRun, RecallKeys.DrillsRead },
        { "Drills", "id=\"rc-sign-modal\"", RecallKeys.DrillsRun, RecallKeys.DrillsRead },
        { "Settings", "id=\"rc-set-save\"", RecallKeys.SettingsManage, RecallKeys.SettingsRead },
        { "Settings", "id=\"rc-set-reason\"", RecallKeys.SettingsManage, RecallKeys.SettingsRead },
        { "Settings", "id=\"rc-sign-modal\"", RecallKeys.SettingsManage, RecallKeys.SettingsRead },
        { "CaseDetails", "id=\"rc-evidence-link\"", RecallKeys.EvidenceExport, RecallKeys.CasesRead }
    };

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task A_control_is_drawn_only_for_the_holder_of_its_key(string view, string control, string key, string pageKey)
    {
        var model = view == "CaseDetails" ? RunId : null;
        var all = typeof(RecallKeys).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToArray();
        var without = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Recall/{view}.cshtml", model, all.Where(k => k != key).ToArray());
        Assert.DoesNotContain(control, without, StringComparison.Ordinal);
        Assert.DoesNotContain("diten-access-denied", without, StringComparison.Ordinal);
        var with = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Recall/{view}.cshtml", model, pageKey, key);
        Assert.Contains(control, with, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_the_save_key_the_settings_are_read_only_and_the_signature_policy_is_not_a_setting_here()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/Settings.cshtml", null, RecallKeys.SettingsRead);
        foreach (var id in new[] { "rc-set-target", "rc-set-freq", "rc-set-e1", "rc-set-e2", "rc-set-e3" })
            Assert.Matches(new Regex($@"id=""{id}""[^>]*\sdisabled"), html);
        Assert.Contains("MOD-0022", html, StringComparison.Ordinal); // D-10: the note says where the e-signature policy lives
        Assert.DoesNotContain("Yalnız kapanış", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Otomatik iste", html, StringComparison.Ordinal); // D-1
    }

    [Fact]
    public async Task The_drill_page_draws_no_demo_controls_and_the_returns_show_their_quarantine_record()
    {
        var drills = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/Drills.cshtml", null, RecallKeys.DrillsRead, RecallKeys.DrillsRun, RecallKeys.DrillsManage);
        Assert.DoesNotContain("+30", drills, StringComparison.Ordinal);      // D-14: no "+30 dk (demo)"
        var script = Code(File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Recall", "drills.js")))
                     + Code(File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Recall", "drill-view.js")));
        Assert.DoesNotContain("Date.now", script, StringComparison.Ordinal); // the clock is the server's
        Assert.DoesNotContain("new Date()", script, StringComparison.Ordinal);

        var detail = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/CaseDetails.cshtml", RunId, RecallKeys.CasesRead);
        var head = Regex.Match(detail, @"id=""rc-ret-links""[\s\S]*?</thead>").Value;
        Assert.Equal(7, Regex.Matches(head, "<th[ >]").Count); // R7a's six + the quarantine record (D-28)
        var caseScript = Code(File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Recall", "case-detail.js")));
        Assert.Contains("/Quarantine/Details/${encodeURIComponent(l.holdId)}", caseScript, StringComparison.Ordinal); // D-28
    }

    // ── the doors ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_drill_doors_carry_the_services_clock_not_the_webs_or_the_browsers()
    {
        var serviceTime = new DateTimeOffset(2026, 10, 15, 9, 30, 0, TimeSpan.Zero);
        var gateway = new FakeGateway
        {
            Answer = r =>
            {
                // FU-CLEAN2 (FU-WEB-CONTRACT-FAKES-RECALL): a whole RECALL 1.9.0 DrillRun (was a 3-member fragment), and the list door gets a DrillRunPage
                var drill = $$"""
                    {"runId":"{{RunId}}","number":"RC-2026-D001","planId":"00000007-0000-4000-8000-000000000007","status":"Running","selectionMode":"Manual",
                     "lot":{"lotId":"l1","lotNumber":"L-1"},"startedAtUtc":"2026-10-15T08:00:00Z","steps":[],"targetHours":4,"version":3}
                    """;
                var json = r.RequestUri!.AbsolutePath.EndsWith("/drill-runs", StringComparison.Ordinal)
                    ? $$"""{"items":[{{drill}}],"total":1,"page":1,"pageSize":10}""" : drill;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                response.Headers.Date = serviceTime;
                return response;
            }
        };
        var run = (ContentResult)await Build(gateway, [RecallKeys.DrillsRead]).DrillRun(RunId, CancellationToken.None);
        Assert.Equal("2026-10-15T09:30:00Z", JsonNode.Parse(run.Content!)!["serverNowUtc"]!.GetValue<string>());
        Assert.Equal(3, JsonNode.Parse(run.Content!)!["data"]!["version"]!.GetValue<int>());
        var list = (ContentResult)await Build(gateway, [RecallKeys.DrillsRead]).DrillRuns(CancellationToken.None);
        Assert.Equal("2026-10-15T09:30:00Z", JsonNode.Parse(list.Content!)!["serverNowUtc"]!.GetValue<string>());
    }

    public static readonly TheoryData<string, string, string, string> Commands = new()
    {
        { "StartDrill", "", "POST", "/api/recall/drill-runs" },
        { "CompleteDrillStep", RunId, "POST", $"/api/recall/drill-runs/{RunId}/steps/Scope" },
        { "PrepareDrillResult", RunId, "POST", $"/api/recall/drill-runs/{RunId}/pending-result" },
        { "SignDrillResult", RunId, "POST", $"/api/recall/drill-runs/{RunId}/result" },
        { "CancelDrill", RunId, "POST", $"/api/recall/drill-runs/{RunId}/cancel" },
        { "CreateDrillPlan", "", "POST", "/api/recall/drill-plans" },
        { "UpdateDrillPlan", RunId, "PUT", $"/api/recall/drill-plans/{RunId}" },
        { "CreateEvidenceExport", "", "POST", "/api/recall/evidence/exports" },
        { "PrepareSettings", "", "POST", "/api/recall/settings/pending-change" },
        { "SaveSettings", "", "PUT", "/api/recall/settings" }
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task A_command_door_forwards_its_operation_with_the_body_key_and_If_Match(string door, string id, string method, string path)
    {
        var gateway = new RecallCaseDetailWebTests.Recorder();
        var controller = Build(gateway, Gates[door]);
        var request = controller.ControllerContext.HttpContext.Request;
        request.Method = "POST";
        var bytes = Encoding.UTF8.GetBytes("{\"idempotencyKey\":\"r7-key-0042\",\"reason\":\"Ic denetim bulgusu IA-2026-07\"}");
        request.Body = new MemoryStream(bytes);
        request.ContentLength = bytes.Length;
        request.Headers.IfMatch = "W/\"4\"";
        var action = typeof(RecallController).GetMethod(door)!;
        var arguments = action.GetParameters().Select(p => p.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None : p.Name == "step" ? "Scope" : id).ToArray();
        var result = await (Task<IActionResult>)action.Invoke(controller, arguments)!;
        Assert.Equal(200, Status(result));
        var sent = Assert.Single(gateway.Sent);
        Assert.Equal((path, method, "r7-key-0042", "W/\"4\""), (sent.Path, sent.Method, sent.Key, sent.IfMatch));
        Assert.Equal("Ic denetim bulgusu IA-2026-07", JsonDocument.Parse(sent.Body!).RootElement.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("Trace/../../settings")]
    [InlineData("Close")]
    [InlineData("")]
    public async Task A_step_outside_the_three_never_reaches_the_gateway(string step)
    {
        var gateway = new FakeGateway();
        Assert.Equal(400, Status(await Build(gateway, [RecallKeys.DrillsRun]).CompleteDrillStep(RunId, step, CancellationToken.None)));
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task The_evidence_file_door_returns_the_services_bytes_and_media_type_unchanged()
    {
        var pdf = "%PDF-1.7 recall evidence"u8.ToArray();
        var gateway = new FakeGateway
        {
            Answer = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pdf) { Headers = { ContentType = new("application/pdf"), ContentDisposition = new("attachment") { FileName = "RC-2026-0001_kanit_ab12cd34.pdf" } } }
            }
        };
        var file = Assert.IsType<FileContentResult>(await Build(gateway, [RecallKeys.EvidenceExport]).EvidenceFile(RunId, CancellationToken.None));
        Assert.Equal(pdf, file.FileContents);
        Assert.Equal(("application/pdf", "RC-2026-0001_kanit_ab12cd34.pdf"), (file.ContentType, file.FileDownloadName));
        Assert.Equal($"/api/recall/evidence/exports/{RunId}/file", Assert.Single(gateway.Requests));
    }

    [Fact]
    public void Every_command_door_targets_a_recall_operation()
    {
        var controller = File.ReadAllText(Path.Combine(Web, "Controllers", "RecallController.cs"));
        var targets = Regex.Matches(controller, @"CommandAsync\(HttpMethod\.\w+,\s*\$""(?<p>[^""]*)""").Select(m => m.Groups["p"].Value).ToList();
        Assert.Equal(10, targets.Count);
        Assert.All(targets, p => Assert.StartsWith("{ApiBase}/", p, StringComparison.Ordinal));
    }

    // ── vocabulary ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_word_the_evidence_preview_can_send_and_every_runtime_prefix_is_delivered()
    {
        var keys = Diten.Web.Views.Recall.Recall.Keys.ToHashSet(StringComparer.Ordinal);
        // WP-MVP1-FIX-LIVE2: the service words the pack from its own resx family (CT "çeviri kaynağı B"); every Recall.Ev.W word it can send is
        // delivered to the page (values: EvidenceTextParityTests)
        var service = System.Xml.Linq.XDocument.Load(Path.Combine(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src",
            "Diten.InventoryService.Application", "Features", "Recall", "Evidence", "RecallEvidenceText_en.resx"));
        var words = service.Root!.Elements("data").Select(d => (string)d.Attribute("name")!).Where(k => k.StartsWith("Recall.Ev.W.", StringComparison.Ordinal)).ToList();
        Assert.True(words.Count > 120, string.Join(",", words));
        Assert.All(words, w => Assert.Contains(w, keys));

        foreach (var step in new[] { "Trace", "Scope", "CustomerList" })
            foreach (var prefix in new[] { "Recall.Drills.Step.", "Recall.Drills.StepInfo.", "Recall.Drills.StepButton." }) Assert.Contains(prefix + step, keys);
        foreach (var f in new[] { "Yearly1", "Yearly2", "Quarterly", "CustomDays" }) Assert.Contains("Recall.Freq." + f, keys);
        foreach (var s in new[] { "RandomFinishedGoodLot", "RawMaterialLot", "SelectedLot" }) Assert.Contains("Recall.DrillScope." + s, keys);
        foreach (var f in new[] { "drillTargetHours", "drillFrequencyWarningDays", "effectivenessTargetPercent_1", "effectivenessTargetPercent_2", "effectivenessTargetPercent_3", "traceMaxDepth", "traceMaxNodes" })
            Assert.Contains("Recall.Settings.Field." + f, keys);
        foreach (var code in new[] { "RECALL_DRILL_ALREADY_RUNNING", "RECALL_DRILL_NO_CANDIDATE", "RECALL_DRILL_STEP_ORDER", "RECALL_DRILL_NOT_FOUND", "RECALL_SETTINGS_INVALID",
                     "RECALL_NO_CHANGES", "RECALL_EVIDENCE_RANGE_INVALID", "RECALL_EVIDENCE_EXPORT_NOT_FOUND" })
            Assert.Contains("Recall.Errors." + code, keys);
    }
}
