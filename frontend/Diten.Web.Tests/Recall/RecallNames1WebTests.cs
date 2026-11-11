using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-NAMES1 (RECALL 1.8.0) — the Web side of the name rule: a person who DID an action is drawn with the name the service sent (the name
/// at the time of the action) or "unknown user"; the Web fills the CURRENT account label (AuthService display-label) only into the ASSIGNED
/// person — the drill plan owner. Measured on the shipped controller and scripts.
/// </summary>
public sealed class RecallNames1WebTests
{
    private static string Controller()
        => File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "Controllers", "RecallController.cs"));

    [Fact]
    public void The_live_label_fill_is_called_only_for_the_drill_plans()
    {
        var source = Controller();
        var calls = Regex.Matches(source, @"await PeopleAsync\(ct, (\w+)\);");
        var call = Assert.Single(calls);
        Assert.Equal("plans", call.Groups[1].Value);
        var plans = source.IndexOf("public async Task<IActionResult> DrillPlans(", StringComparison.Ordinal);
        var next = source.IndexOf("    public async Task<IActionResult> ", plans + 10, StringComparison.Ordinal);
        Assert.True(call.Index > plans && call.Index < next, "the one live label fill is inside DrillPlans");
    }

    [Fact]
    public async Task The_recall_person_rule_holds_against_the_shipped_files()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-names1.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 3$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
