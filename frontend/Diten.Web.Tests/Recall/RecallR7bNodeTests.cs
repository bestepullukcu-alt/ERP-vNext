using System.Diagnostics;
using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// The bridge that makes `dotnet test` run the R7b Node tests against the SHIPPED Tatbikat Planlayıcı / Ayarlar / Kanıt Paketi scripts (the
/// RecallR7aNodeTests pattern: a missing `node` fails here instead of skipping): the drill clock is the server's (the browser clock is poisoned in
/// the test — R7b sabotage (c)), the steps go in the server's order, the cancel and the settings need a reason, the result and the settings are
/// prepare → MOD-0022 → command, the evidence download is verified against the logged SHA-256.
/// </summary>
public sealed class RecallR7bNodeTests
{
    [Fact]
    public async Task The_drill_settings_and_evidence_scripts_hold_against_the_shipped_files()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-r7b.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 10$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
