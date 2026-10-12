using System.Text.RegularExpressions;
using Diten.Web.Models.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// Pack §14 "every button present only with its key", measured against the SOURCES, not a list typed here:
/// <list type="number">
/// <item>the Web's button table (<see cref="TraceActions"/>) equals the actions the LOT-SERIAL-TRACKING manifest provider
/// declares — code and key (FU-0174-CAT-01: the ledger-opening buttons carry <c>trace.lots.read</c>);</item>
/// <item>the scripts draw buttons by ACTION CODE only (<c>TraceUi.can('CODE')</c>), every code they use is in the table,
/// and no script names a permission key;</item>
/// <item>the server-drawn buttons of Lot Detayı / the receipt modal / İzlenebilirlik appear for exactly the user holding
/// their key;</item>
/// <item>CT-D5a: the quick lot is not a manifest action anywhere, and the INVENTORY-LEDGER manifest declares no
/// <c>trace.*</c> key.</item>
/// </list>
/// </summary>
public sealed class TraceButtonsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string ServiceApi = Path.Combine(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src", "Diten.InventoryService.Api");
    private static readonly string Scripts = Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Traceability");

    private readonly WebApplicationFactory<Program> _factory;

    public TraceButtonsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void The_button_table_is_the_manifest_s_action_table()
    {
        var keys = Regex.Matches(File.ReadAllText(Path.Combine(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src",
                "Diten.InventoryService.Infrastructure", "Authorization", "TracePermissions.cs")),
                @"public const string (?<name>\w+) = ""(?<key>[^""]+)""")
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["key"].Value);
        var manifest = File.ReadAllText(Path.Combine(ServiceApi, "ModuleRegistration", "LotSerialTrackingManifestProvider.cs"));
        var declared = Regex.Matches(manifest, @"new ModuleManifestAction\(""(?<code>[A-Z]+)"", ""[^""]*"", (?<perm>[\w.]+),")
            .Select(m => (Code: m.Groups["code"].Value, Key: m.Groups["perm"].Value.StartsWith("TracePermissions.", StringComparison.Ordinal)
                ? keys[m.Groups["perm"].Value["TracePermissions.".Length..]]
                : "FOREIGN:" + m.Groups["perm"].Value))
            .Distinct()
            .ToDictionary(a => a.Code, a => a.Key);
        Assert.True(declared.Count >= 20, "the manifest parser found too few actions: " + declared.Count);

        Assert.Equal(declared.OrderBy(p => p.Key), TraceActions.KeyOf.OrderBy(p => p.Key));
        Assert.All(TraceActions.KeyOf.Values, k => Assert.StartsWith("trace.", k));
        Assert.DoesNotContain("NEWLOTQUICK", TraceActions.KeyOf.Keys); // CT-D5a: an in-form control, not a manifest action
    }

    [Fact]
    public void The_scripts_draw_buttons_by_action_code_only()
    {
        var sources = Directory.EnumerateFiles(Scripts, "*.js", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(Scripts, f), File.ReadAllText);
        var used = sources.Values.SelectMany(s => Regex.Matches(s, @"can\('(?<code>[A-Z]+)'\)|key: '(?<code>[A-Z]+)'|key: edit").Select(m => m.Groups["code"].Value))
            .Where(c => c.Length > 0).Distinct().ToList();
        Assert.True(used.Count >= 8, "too few codes found in the scripts: " + string.Join(",", used));
        Assert.All(used, code => Assert.True(TraceActions.KeyOf.ContainsKey(code), $"script uses {code}, which is not a pack §14 action"));
        Assert.All(sources, s => Assert.DoesNotMatch(@"['""]trace\.[a-z]+\.[a-z-]+['""]", s.Value));
        // No row action without a visibility decision: every renderActions item carries `visible: …can(`.
        Assert.All(sources, s => Assert.DoesNotMatch(@"\{ key: '[A-Z]+', icon", s.Value));
    }

    [Fact]
    public async Task Lot_detail_buttons_appear_exactly_for_their_keys()
    {
        var model = LotPageViewModel.From(Guid.NewGuid().ToString());
        const string view = "/Views/Traceability/Lots/Details.cshtml";
        string[] detailCodes = ["EDITLOT", "EDITRELEASEDLOT", "CHANGELOTSTATUS", "LOTMOVES", "LOTTRACEOPEN", "OPENINLEDGER", "VIEWALLSERIALS"];

        var readOnly = await TraceViewRenderer.RenderAsync(_factory, view, model, "trace.lots.read");
        Assert.Equal(["LOTMOVES", "LOTTRACEOPEN", "OPENINLEDGER"], Drawn(readOnly, detailCodes));

        foreach (var code in new[] { "EDITLOT", "EDITRELEASEDLOT", "CHANGELOTSTATUS", "VIEWALLSERIALS" })
        {
            var html = await TraceViewRenderer.RenderAsync(_factory, view, model, "trace.lots.read", TraceActions.KeyOf[code]);
            Assert.Contains(code, Drawn(html, detailCodes));
            Assert.Equal(new[] { "LOTMOVES", "LOTTRACEOPEN", "OPENINLEDGER", code }.OrderBy(c => c), Drawn(html, detailCodes).OrderBy(c => c));
        }

        // The status dialog's template and its anti-forgery token exist only for a user who may change a status.
        Assert.DoesNotContain("lot-status-template", readOnly, StringComparison.Ordinal);
        var changer = await TraceViewRenderer.RenderAsync(_factory, view, model, "trace.lots.read", "trace.lots.change-status");
        Assert.Contains("lot-status-template", changer, StringComparison.Ordinal);
        Assert.Contains("lots-antiforgery", changer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_receipt_and_trace_buttons_follow_their_keys_and_the_page_hands_the_script_its_table()
    {
        var movements = await TraceViewRenderer.RenderAsync(_factory, "/Views/Traceability/LotMovements/Index.cshtml", LotPageViewModel.From(null), "trace.lots.read", "inventory.stock.read");
        Assert.Contains("data-trace-action=\"RECEIPTINLEDGER\"", movements, StringComparison.Ordinal);
        var withoutLedger = await TraceViewRenderer.RenderAsync(_factory, "/Views/Traceability/LotMovements/Index.cshtml", LotPageViewModel.From(null), "trace.lots.read");
        Assert.Contains("lotmovements-no-ledger", withoutLedger, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-lotmovements", withoutLedger, StringComparison.Ordinal);

        var trace = await TraceViewRenderer.RenderAsync(_factory, "/Views/Traceability/LotTrace/Index.cshtml", LotPageViewModel.From(null), "trace.lots.read");
        Assert.Contains("data-trace-action=\"TRACELOTDETAIL\"", trace, StringComparison.Ordinal);
    }

    [Fact]
    public void CT_D5a_the_INVENTORY_LEDGER_manifest_declares_no_trace_key_and_no_quick_lot_action()
    {
        var ledger = File.ReadAllText(Path.Combine(ServiceApi, "ModuleRegistration", "InventoryLedgerManifestProvider.cs"));
        Assert.DoesNotContain("trace.", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("TracePermissions", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("NEWLOTQUICK", ledger, StringComparison.Ordinal);
    }

    private static List<string> Drawn(string html, string[] codes) =>
        codes.Where(c => html.Contains($"data-trace-action=\"{c}\"", StringComparison.Ordinal)).ToList();
}
