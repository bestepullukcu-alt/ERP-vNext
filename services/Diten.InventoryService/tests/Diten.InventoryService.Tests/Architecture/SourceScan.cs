using System.Text;
using Diten.InventoryService.Tests.TestInfrastructure;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>Reads the service's PRODUCTION sources (never the tests) with comments stripped, for text-level guards.</summary>
public static class SourceScan
{
    public sealed record SourceFile(string RelativePath, string Body);

    public static IReadOnlyList<SourceFile> Production(string? layer = null)
    {
        var src = Path.Combine(RepoPaths.ServiceRoot, "src");
        var root = layer is null ? src : Path.Combine(src, layer);
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(p => new SourceFile(Path.GetRelativePath(src, p).Replace('\\', '/'), WithoutComments(File.ReadAllText(p))))
            .ToList();
        if (files.Count == 0)
        {
            throw new InvalidOperationException($"Source scan found nothing under {root} — the guard would be vacuous.");
        }

        return files;
    }

    /// <summary>Strips // and /* */ comments but keeps string literals (a collection name lives in one).</summary>
    public static string WithoutComments(string source)
    {
        var output = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                output.Append('\n');
                continue;
            }

            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i++;
                continue;
            }

            if (c == '"')
            {
                output.Append(c);
                i++;
                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '\\' && i + 1 < source.Length) { output.Append(source[i]).Append(source[i + 1]); i += 2; continue; }
                    output.Append(source[i]);
                    i++;
                }

                if (i < source.Length) output.Append('"');
                continue;
            }

            output.Append(c);
        }

        return output.ToString();
    }
}
