using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (live finding 1) — the movement history's LOT cell shows the lot NUMBER: the shipped index.js draws it (Node,
/// stockmovements-lot.test.mjs: number / link / "unknown lot" with the id only as the title / typed text), and the page delivers the
/// "unknown lot" word in 7 languages through its own L10n bridge.
/// </summary>
public sealed class StockMovementsLotTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task The_lot_cell_rule_holds_against_the_shipped_script()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "stockmovements-lot.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 3$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }

    [Fact]
    public async Task The_history_page_delivers_the_unknown_lot_word_and_seven_languages_carry_it()
    {
        var html = await TraceViewRenderer.RenderAsync(factory, "/Views/Inventory/StockMovements/Index.cshtml", null, "inventory.stock.read");
        Assert.Contains("\"unknownLot\":", html, StringComparison.Ordinal);
        var dir = Path.Combine(TraceViewRenderer.WebRoot, "Resources", "Views", "Inventory", "StockMovements");
        var values = new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" }.ToDictionary(l => l, l => System.Xml.Linq.XDocument.Load(Path.Combine(dir, $"StockMovementsIndex.{l}.resx")).Root!
            .Elements("data").Single(d => (string)d.Attribute("name")! == "UnknownLot").Element("value")!.Value);
        Assert.All(values.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
        Assert.Equal(7, values.Values.Distinct().Count());
        Assert.Equal("Bilinmeyen lot", values["tr"]);
    }
}
