using System.Diagnostics;
using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// The bridge that makes `dotnet test` run the R6 Node tests against the SHIPPED recall scripts (the ExpiryF5NodeTests pattern: a missing
/// `node` fails here instead of skipping): tree = table, the CT-R8 count, the drill row (D-25), the panel cards = the server's numbers.
/// </summary>
public sealed class RecallR6NodeTests
{
    [Fact]
    public async Task The_recall_pages_hold_against_the_shipped_scripts()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-r6.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 5$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
