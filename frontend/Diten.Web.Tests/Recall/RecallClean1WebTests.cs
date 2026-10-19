using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — the bridge that makes `dotnet test` run recall-clean1.test.mjs (FU-0177-HEAD-NAMES) against the SHIPPED scripts, and
/// the vocabulary of the rule ("unknown user" delivered to the page in 7 languages through the key list; parity: RecallWebTests).
/// </summary>
public sealed class RecallClean1NodeTests
{
    [Fact]
    public async Task The_case_page_person_rule_holds_against_the_shipped_files()
    {
        Assert.Contains("Recall.Case.UnknownUser", Diten.Web.Views.Recall.Recall.Keys);
        foreach (var key in new[] { "Recall.Drills.OwnerPick", "Recall.Drills.OwnerSearch", "Recall.Drills.OwnerFind", "Recall.Drills.OwnerNone" }) Assert.Contains(key, Diten.Web.Views.Recall.Recall.Keys); // FU-0177-PLAN-OWNER
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-clean1.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 3$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
