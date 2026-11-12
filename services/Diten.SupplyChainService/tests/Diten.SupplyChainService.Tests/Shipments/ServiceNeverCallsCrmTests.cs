using System.Text.RegularExpressions;
using Xunit;

namespace Diten.SupplyChainService.Tests.Shipments;

/// <summary>
/// MOD-0183 r9 §24.1 / SB-2a (VER F-2, 2026-11-05): the service checks the customerRef format and never calls CRM. Adding a CRM
/// client, a CRM route, a CRM base-URL setting or a project reference to the CRM service anywhere in the service turns this red;
/// comments that name CRM are not code and are ignored.
/// </summary>
public sealed class ServiceNeverCallsCrmTests
{
    private static readonly Regex Forbidden = new(@"/api/crm|CrmService|CrmClient|ICrm[A-Z]|""Crm[:""]|Crm__|crm\.account", RegexOptions.CultureInvariant);

    [Fact]
    public void NoServiceSourceConfigurationOrProjectReachesCrm()
    {
        var src = Path.Combine(Root(), "services", "Diten.SupplyChainService", "src");
        var files = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".json", StringComparison.Ordinal) || f.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(files);
        var hits = files.SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal) && Forbidden.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(src, x.f)}:{x.i + 1}: {x.line.Trim()}").ToList();
        Assert.True(hits.Count == 0, string.Join(Environment.NewLine, hits));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }
}
