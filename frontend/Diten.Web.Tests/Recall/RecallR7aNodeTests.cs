using System.Diagnostics;
using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// The bridge that makes `dotnet test` run the R7a Node tests against the SHIPPED Vaka Detayı scripts (the RecallR6NodeTests pattern: a missing
/// `node` fails here instead of skipping): every button needs its key and its status, four-eyes on the screen (CT-R1), and the signed decisions
/// are prepare → MOD-0022 signature → command (pack §8.8) — R7a sabotages (b) and (c).
/// </summary>
public sealed class RecallR7aNodeTests
{
    [Fact]
    public async Task The_case_detail_scripts_hold_against_the_shipped_files()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-r7a.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 5$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
