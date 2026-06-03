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

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
