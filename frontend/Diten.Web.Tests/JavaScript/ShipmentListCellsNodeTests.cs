using System.Diagnostics;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

/// <summary>MOD-0183 r9 DL-4: runs Node/shipment-list-cells.test.mjs against the shipped index.js (CI runs only dotnet test).</summary>
public sealed class ShipmentListCellsNodeTests
{
    [Fact]
    public async Task TheListCellsShowNamesNotIds()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = Root(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "shipment-list-cells.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, output + await error);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }
}
