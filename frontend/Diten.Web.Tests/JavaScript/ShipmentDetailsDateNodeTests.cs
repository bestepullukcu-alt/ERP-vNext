using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

/*
 * Q382 — the bridge that makes CI run the Node date tests. CI runs `dotnet test frontend/Diten.Web.Tests`
 * (scripts/run_phase1_gates.sh:42) and nothing else for the frontend, so a Node test that only a person runs is not a
 * guard. Each zone is its own case: an Istanbul failure cannot hide behind a green UTC run, and a missing `node`
 * fails here instead of skipping (Process.Start throws).
 *
 * Unlike the other tests in this folder, this one EXECUTES details.js — it does not read its text. What it still does
 * not execute is the DOM; see ../README.md.
 */
public sealed class ShipmentDetailsDateNodeTests
{
    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("UTC")]
    public async Task DateRoundTripHoldsInZone(string zone)
    {
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = Root(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "shipment-details-datetime.test.mjs"));
        start.Environment["TZ"] = zone;

        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        var report = $"TZ={zone}\n{output}\n{await error}";

        Assert.True(node.ExitCode == 0, report);
        // A run that found no tests also exits 0; require the six cases the file defines.
        Assert.Matches(new Regex(@"^# pass 6$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (AGENTS.md) not found.");
    }
}
