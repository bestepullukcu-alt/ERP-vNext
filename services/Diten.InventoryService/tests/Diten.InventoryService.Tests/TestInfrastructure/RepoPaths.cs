namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Locates the repo and the frozen contracts. Contracts are found BY FILE NAME under the repo's docs tree rather than
/// by a hard-coded folder path, so the tests read the one frozen file itself (never a copy that could drift) and do
/// not pin a docs folder layout (DocsPathGuardTests).
/// </summary>
public static class RepoPaths
{
    public static string Root
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
                current = current.Parent;
            }

            throw new InvalidOperationException("Repo root not found (no AGENTS.md above the test binary).");
        }
    }

    public static string ServiceRoot => Path.Combine(Root, "services", "Diten.InventoryService");

    // docs/records keeps evidence copies of contracts (e.g. a release pack's publication tree); they are records, never the
    // frozen contract, so they are not candidates.
    public static string Contract(string fileName)
    {
        var records = Path.Combine(Root, "docs", "records") + Path.DirectorySeparatorChar;
        var matches = Directory.EnumerateFiles(Path.Combine(Root, "docs"), fileName, SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(records, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException($"Expected exactly one '{fileName}' under the docs tree, found {matches.Count}.");
    }
}
