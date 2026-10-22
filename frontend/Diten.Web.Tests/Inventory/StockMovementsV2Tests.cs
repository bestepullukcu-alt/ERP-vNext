using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (f) — the movement history (MOD-0173-FU01 S-2) reaches its table through the DataTables v2 API only: the verifier's
/// "jQuery DataTable plugin" gap is closed in the shipped script, with the list's behaviour kept (server paging, no sorting / search, the
/// responsive control column, "Kaydeden", the row actions). Measured by Node against the shipped index.js (stockmovements-v2.test.mjs).
/// </summary>
public sealed class StockMovementsV2Tests
{
    [Fact]
    public async Task The_history_uses_the_DataTables_v2_api_and_keeps_its_behaviour()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "stockmovements-v2.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 3$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
