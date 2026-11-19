using System.Diagnostics;
using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-3 — runs <c>JavaScript/Node/warehouse-putaway-rules.test.mjs</c> against the SHIPPED <c>rules.js</c> / <c>index.js</c> of Yerleştirme
/// Görevleri: menu rule (AC-43), Pending lock (AC-39), flag badge (AC-46), exact bin code (AC-44), locked candidate (AC-41), suggestion (AC-42),
/// quantity stepper (AC-45). Every test of the file must pass.
/// </summary>
public sealed class WarehousePutawayRulesNodeTests
{
    [Fact]
    public async Task The_putaway_screen_rules_hold_against_the_shipped_scripts()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "warehouse-putaway-rules.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 10$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
