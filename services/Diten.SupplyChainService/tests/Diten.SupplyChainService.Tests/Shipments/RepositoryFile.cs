namespace Diten.SupplyChainService.Tests.Shipments;

// Locates a repository file by walking up from the test binaries, so a test reads the authority itself
// (contract, module pack) instead of a copy of its values that could drift with it.
internal static class RepositoryFile
{
    public static string Locate(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Repository file not found above {AppContext.BaseDirectory}.", relativePath);
    }
}
