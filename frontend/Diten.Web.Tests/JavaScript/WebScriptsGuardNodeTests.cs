using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

/// <summary>
/// WP-MVP1-FU-CLEAN1 (FU-QUI1-ESC-TEST, FU-QUI1-LOTSTATUS) — the bridge that makes `dotnet test` run web-scripts-guard.test.mjs against the
/// SHIPPED page scripts: every script parses (Node's own parser, no package), and in the MVP-1 modules the global confirm's TEXT options are
/// neither escaped by the caller nor markup (a form body goes through subtextHtml).
/// </summary>
public sealed class WebScriptsGuardNodeTests
{
    [Fact]
    public async Task Every_page_script_parses_and_the_confirm_text_options_hold()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "web-scripts-guard.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 4$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
