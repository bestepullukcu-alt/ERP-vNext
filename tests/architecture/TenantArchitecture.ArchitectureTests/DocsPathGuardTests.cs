using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — NO CODE FILE POINTS INTO docs/ OUTSIDE THE FIVE FOLDERS (2026-09-10).
 *
 * WHY THIS FILE EXISTS. The 2026-09-07 docs reorganisation (9d8551e1) broke CODE four times, not documents:
 * a Python gate (.py), a CSS comment (.css), DocumentReferenceListTests (.cs — ten tests red on main) and two
 * PowerShell scripts (.ps1). The fixes are in 9c5119c1. `.antigravity/rules/docs-organization.md` §4 step 4
 * already said "scan the code extensions too" — but it was a step somebody had to REMEMBER, and its list
 * did not even name .ps1. A rule that is written is not a rule that runs; this is the running version.
 *
 * THE RULE. After the reorganisation `docs/` holds exactly five folders — vendor · records · roadmap · guides
 * · reference. Any code-side path naming another folder directly under it points at something that was moved
 * or never existed, and it fails the day somebody runs it rather than the day the docs moved.
 *
 * WHAT IS MATCHED — the two shapes the four breakages actually had:
 *   • a slashed path whose first segment under docs is followed by a separator: docs/‹x›/… or docs\‹x›\…
 *     (a FOLDER; a file sitting at the docs root is a different question and not this guard's);
 *   • Path.Combine/Path.Join arguments: …, "docs", "‹x›", … — again only when another argument follows,
 *     and only when ‹x› looks like a folder name (no spaces): `"docs", "Supporting documents attached",` is
 *     a UI label pair in DemandIdeaCapturePageMapper, not a path, and it was the first false positive.
 * Comments are NOT stripped: the CSS breakage WAS a comment, and a stale path in a comment sends the next
 * reader to a folder that does not exist.
 *
 * ⚠ THIS FILE'S OWN EXAMPLES use ‹x›, which is not a name character — prose about the rule cannot
 * trip it, and the scan still reads this file like any other.
 *
 * WHAT IS NOT MATCHED, deliberately:
 *   • a SERVICE'S OWN docs folder (services/Diten.AuthService/docs/…) — discovered from disk, not listed, so
 *     a new one is recognised without editing this file;
 *   • generated or third-party trees: obj/ bin/ node_modules/ wwwroot/assets/vendor/ frontend/_Reference/,
 *     and tool state (.git, .git-backups, .claude, .logs, .vscode). `.antigravity` IS scanned — the Python
 *     gate that broke lives there.
 *
 * ⚠ IT READS TEXT, NOT A SYNTAX TREE. A path assembled across statements (var d = "docs"; … d + "/x/")
 * evades it. None of the four breakages had that shape; if one ever does, that is the upgrade.
 */
public class DocsPathGuardTests
{
    private static readonly HashSet<string> Five = new(StringComparer.Ordinal)
    {
        "vendor", "records", "roadmap", "guides", "reference"
    };

    /// <summary>The extensions the 2026-09-07 breakages lived in, and every other one code is written in here.</summary>
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cshtml", ".js", ".py", ".sh", ".ps1", ".json", ".csproj", ".css", ".html",
        ".yaml", ".yml", ".xml", ".resx"
    };

    private static readonly HashSet<string> PrunedDirectoryNames = new(StringComparer.Ordinal)
    {
        "obj", "bin", "node_modules", ".git", ".git-backups", ".claude", ".logs", ".vscode"
    };

    private static readonly string[] PrunedPaths =
    [
        "frontend/Diten.Web/wwwroot/assets/vendor",
        "frontend/_Reference"
    ];

    private const string NameChars = "A-Za-z0-9_.-";

    /* docs/‹x›/  or  docs\‹x›\  — not preceded by a name character, so "mydocs/x/" is not a docs path. */
    private static readonly Regex Slashed = new(
        $@"(?<![{NameChars}])docs[/\\]+(?<x>[{NameChars}]+)[/\\]",
        RegexOptions.Compiled);

    /* …, "docs", "‹x›", … — the trailing comma is what makes x a folder rather than the last (file) segment. */
    private static readonly Regex Combined = new(
        $@"""docs""\s*,\s*@?""(?<x>[{NameChars}]+)""\s*,",
        RegexOptions.Compiled);

    /* What sits immediately before a match — the parent folder, if the path names one. */
    private static readonly Regex SlashedParent = new($@"(?<p>[{NameChars}]+)[/\\]+$", RegexOptions.Compiled);
    private static readonly Regex CombinedParent = new(@"""(?<p>[^""]+)""\s*,\s*$", RegexOptions.Compiled);

    [Fact]
    public void NoCodeFilePointsIntoDocsOutsideTheFiveFolders()
    {
        var root = RepoRoot();
        var files = CodeFiles(root).ToList();
        var nestedDocsParents = NestedDocsParents(root);

        var offenders = new List<string>();
        var honoured = 0;

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var hit in Hits(lines[i], nestedDocsParents))
                {
                    if (Five.Contains(hit))
                    {
                        honoured++;
                        continue;
                    }

                    offenders.Add($"{relative}:{i + 1} → docs/{hit} — bkz. docs-organization.md §4");
                }
            }
        }

        /*
         * ⚠ PRESENCE FIRST. "No violations" from a scan that read nothing is the vacuous green this guard
         * exists to replace. So: it must have read the extension that was missing from the written rule, the
         * language that broke ten tests, and at least one LEGITIMATE docs path — proof the patterns fire.
         */
        Assert.Contains(files, f => f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.True(honoured > 0, "the scan matched no docs path at all — the patterns are not firing");

        Assert.True(offenders.Count == 0,
            "code points into docs/ outside vendor · records · roadmap · guides · reference. "
            + "Each of these fails the day it runs, not the day the docs moved:\n"
            + string.Join("\n", offenders));
    }

    /// <summary>Every docs folder a line names — minus the ones that belong to a service's own docs tree.</summary>
    private static IEnumerable<string> Hits(string line, IReadOnlySet<string> nestedDocsParents)
    {
        foreach (Match m in Slashed.Matches(line))
        {
            var parent = SlashedParent.Match(line[..m.Index]);
            if (parent.Success && nestedDocsParents.Contains(parent.Groups["p"].Value)) continue;
            yield return m.Groups["x"].Value;
        }

        foreach (Match m in Combined.Matches(line))
        {
            var parent = CombinedParent.Match(line[..m.Index]);
            if (parent.Success && nestedDocsParents.Contains(parent.Groups["p"].Value)) continue;
            yield return m.Groups["x"].Value;
        }
    }

    /// <summary>
    /// Folders that carry a docs/ of their own below the root (today: Diten.AuthService, Diten.Platform).
    /// Found on disk so a new one needs no edit here — and so the exclusion can never outlive the folder.
    /// </summary>
    private static IReadOnlySet<string> NestedDocsParents(string root) =>
        Directories(root)
            .Where(d => Path.GetFileName(d) == "docs")
            .Select(d => Path.GetDirectoryName(d)!)
            .Where(parent => !string.Equals(parent, root, StringComparison.Ordinal))
            .Select(parent => Path.GetFileName(parent)!)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> CodeFiles(string root) =>
        Directories(root)
            .Prepend(root)
            .SelectMany(dir => Directory.EnumerateFiles(dir))
            .Where(file => CodeExtensions.Contains(Path.GetExtension(file)));

    /// <summary>Every directory below the root, pruned as it walks — node_modules is never entered, not filtered after.</summary>
    private static IEnumerable<string> Directories(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            foreach (var child in Directory.EnumerateDirectories(pending.Pop()))
            {
                if (PrunedDirectoryNames.Contains(Path.GetFileName(child))) continue;

                var relative = Path.GetRelativePath(root, child).Replace('\\', '/');
                if (PrunedPaths.Any(p => relative == p || relative.StartsWith(p + "/", StringComparison.Ordinal))) continue;

                yield return child;
                pending.Push(child);
            }
        }
    }

    /// <summary>The repository root — the same walk-up-to-AGENTS.md as PlatformSchemaManifestTests.RepoRoot().</summary>
    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
