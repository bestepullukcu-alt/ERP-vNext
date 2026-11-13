using System.Diagnostics;
using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-7 — runs <c>JavaScript/Node/warehouse-s3.test.mjs</c> against the SHIPPED S3 scripts (WarehouseExceptions / WarehouseActivity /
/// WarehouseIntegration <c>index.js</c>): a record links to its screen only on the reader's key (an operator without the outbound read gets no outbound
/// link — Q-10a), a deferred Staging difference says why (r25 F-1), the Integration script fetches nothing. Every test of the file must pass.
/// </summary>
public sealed class WarehouseS3NodeTests
{
    [Fact]
    public async Task The_S3_screen_rules_hold_against_the_shipped_scripts()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "warehouse-s3.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 3$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
