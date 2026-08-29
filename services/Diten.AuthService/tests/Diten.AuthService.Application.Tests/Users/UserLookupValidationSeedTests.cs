using System.Reflection;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Users;

public sealed class UserLookupValidationSeedTests
{
    [Fact]
    public void LookupValidationPermissionIsPresentInSeeder()
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains("auth\", \"users\", \"lookup-validation", seederSource);
    }

    private static string GetDataSeederPath()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve DataSeeder assembly directory.");

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "Seed", "DataSeeder.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        var repoCandidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Diten.AuthService.Persistence",
            "Seed",
            "DataSeeder.cs"));

        if (File.Exists(repoCandidate))
        {
            return repoCandidate;
        }

        // Location-independent fallback: walk up from the (possibly relocated, e.g. -o output) base directory and
        // probe the known repo-relative seed path so the source-based test runs from any build output directory.
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = Path.Combine("services", "Diten.AuthService", "src",
            "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
