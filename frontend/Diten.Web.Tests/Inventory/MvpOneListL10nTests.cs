using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (live finding 2: "Search..." in English on Recall Vakaları and Karantina) — every MVP-1 DataTable page delivers the list
/// component's shared words (the search hint first) in its RENDERED page, before its list script: the five e-signature lists through their
/// own <c>esign-list-l10n</c>, the fifteen others through <c>_MvpOneListL10n</c> (SharedResource). dt-defaults.js falls to English only when
/// <c>window.L10n.Search</c> is empty; SharedResource carries the word in all seven languages. The merge rule (the page's own word wins, a
/// missing one is filled) is measured by Node against the shipped mvp1-list-l10n.js.
/// </summary>
public sealed class MvpOneListL10nTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>view, the page key(s) that draw it (UAS-001, comma-separated), the list script it must precede, the payload id.</summary>
    public static readonly TheoryData<string, string, string, string> Pages = new()
    {
        { "/Views/Inventory/StockLedger/Index.cshtml", "inventory.stock.read", "Inventory/StockLedger/index.js", "mvp1-list-l10n" },
        { "/Views/Inventory/StockMovements/Index.cshtml", "inventory.stock.read", "Inventory/StockMovements/index.js", "mvp1-list-l10n" },
        { "/Views/Inventory/Locations/Index.cshtml", "inventory.locations.read", "Inventory/Locations/index.js", "mvp1-list-l10n" },
        { "/Views/Traceability/Lots/Index.cshtml", "trace.lots.read", "Traceability/Lots/index.js", "mvp1-list-l10n" },
        { "/Views/Traceability/LotMovements/Index.cshtml", "inventory.stock.read,trace.lots.read", "Traceability/LotMovements/index.js", "mvp1-list-l10n" },
        { "/Views/Traceability/LotMigration/Index.cshtml", "trace.lots.migrate", "Traceability/LotMigration/index.js", "mvp1-list-l10n" },
        { "/Views/Traceability/Serials/Index.cshtml", "trace.serials.read", "Traceability/Serials/index.js", "mvp1-list-l10n" },
        { "/Views/Quarantine/Index.cshtml", "quarantine.holds.read", "Quarantine/queue.js", "mvp1-list-l10n" },
        { "/Views/Quarantine/Blocked.cshtml", "quarantine.holds.read", "Quarantine/blocked.js", "mvp1-list-l10n" },
        { "/Views/Quarantine/Retest.cshtml", "quarantine.holds.read", "Quarantine/retest.js", "mvp1-list-l10n" },
        { "/Views/Quarantine/Settings.cshtml", "quarantine.settings.manage", "Quarantine/settings.js", "mvp1-list-l10n" },
        { "/Views/Recall/Cases.cshtml", "recall.cases.read", "Recall/cases.js", "mvp1-list-l10n" },
        { "/Views/Expiry/Approaching.cshtml", "expiry.dashboard.read", "Expiry/approaching.js", "mvp1-list-l10n" },
        { "/Views/Expiry/Exceptions.cshtml", "expiry.exceptions.read", "Expiry/exceptions.js", "mvp1-list-l10n" },
        { "/Views/Expiry/Expired.cshtml", "expiry.dashboard.read", "Expiry/expired.js", "mvp1-list-l10n" },
        { "/Views/ESignature/Requests.cshtml", "esign.requests.read", "ESignature/requests.js", "esign-list-l10n" },
        { "/Views/ESignature/AuditExport.cshtml", "esign.signatures.export", "ESignature/audit-export.js", "esign-list-l10n" },
        { "/Views/ESignature/MySignature.cshtml", "esign.my-samples.manage", "ESignature/my-signature.js", "esign-list-l10n" },
        { "/Views/ESignature/Settings/Meanings.cshtml", "esign.meanings.read", "ESignature/meanings.js", "esign-list-l10n" },
        { "/Views/ESignature/Settings/SampleApprovals.cshtml", "esign.samples.approve", "ESignature/sample-approvals.js", "esign-list-l10n" }
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task The_rendered_list_page_delivers_its_search_hint_before_its_list_script(string view, string pageKey, string script, string payloadId)
    {
        var model = view.Contains("/LotMovements/", StringComparison.Ordinal) || view.Contains("/Serials/", StringComparison.Ordinal)
            ? Diten.Web.Models.Traceability.LotPageViewModel.From(Guid.NewGuid().ToString())
            : null;
        var html = await TraceViewRenderer.RenderAsync(factory, view, model, pageKey.Split(','));
        var payload = Regex.Match(html, $@"<script id=""{payloadId}"" type=""application/json"">\s*(?<json>\{{[\s\S]*?\}})\s*</script>");
        Assert.True(payload.Success, $"{view}: no {payloadId} payload in the rendered page");
        var words = JsonDocument.Parse(payload.Groups["json"].Value).RootElement;
        foreach (var key in new[] { "Search", "Filter", "Apply", "Reset", "ShowAll", "SaveView", "ColumnVisibility" })
            Assert.False(string.IsNullOrWhiteSpace(words.TryGetProperty(key, out var v) ? v.GetString() : null), $"{view}: {key} is empty");

        var at = html.IndexOf($"/assets/js/{script}", StringComparison.Ordinal);
        Assert.True(at > 0, $"{view}: {script} is not loaded");
        Assert.True(payload.Index < at, $"{view}: the list words must come before {script}");
        if (payloadId == "mvp1-list-l10n")
        {
            var merge = html.IndexOf("/assets/js/Inventory/mvp1-list-l10n.js", StringComparison.Ordinal);
            Assert.True(merge > 0 && merge < at, $"{view}: mvp1-list-l10n.js must run before {script}");
        }
    }

    [Fact]
    public void SharedResource_carries_the_list_words_in_seven_languages()
    {
        var values = Languages.ToDictionary(l => l, l => XDocument.Load(Path.Combine(TraceViewRenderer.WebRoot, "Resources", $"SharedResource.{l}.resx")).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty));
        foreach (var key in new[] { "Search", "Filter", "Apply", "Reset", "ShowAll", "SaveView", "ColumnVisibility", "Import" })
        {
            Assert.All(Languages, l => Assert.False(string.IsNullOrWhiteSpace(values[l].GetValueOrDefault(key)), $"{l}: {key}"));
            Assert.NotEqual(values["en"][key], values["tr"][key]);
        }
    }

    [Fact]
    public async Task The_merge_rule_holds_against_the_shipped_script()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "mvp1-list-l10n.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 2$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
