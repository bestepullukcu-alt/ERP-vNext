using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests;

/// <summary>
/// No fixture names a fixed mongod path again (owner 'Evet, düzelt' 2026-11-06). The binary comes from
/// TestInfrastructure/MongodBinary.cs (DITEN_TEST_MONGOD first). A pasted-back /opt/homebrew path works on a Mac and
/// breaks only on CI's Linux runner, so this scan of the project's own sources makes it red on any machine.
/// </summary>
public sealed class FixedMongodPathGuardTests
{
    private const string FixedPath = "/opt/homebrew/bin/" + "mongod";

    [Fact]
    public void No_fixture_names_a_fixed_mongod_path()
    {
        var project = ProjectDirectory();
        var sources = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(project, file))
            .ToArray();
        Assert.True(sources.Length >= 10, $"only {sources.Length} sources read under {project} — the guard is blind");
        var hits = sources
            .Where(file => !file.EndsWith(Path.Combine("TestInfrastructure", "MongodBinary.cs"), StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains(FixedPath, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(project, file))
            .ToArray();
        Assert.True(hits.Length == 0, "fixtures that name a fixed mongod path — use MongodBinary.Location: " + string.Join(", ", hits));
    }

    private static bool IsBuildOutput(string project, string file)
    {
        var first = Path.GetRelativePath(project, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }

    private static string ProjectDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Diten.ManagementGovernanceService.IntegrationTests.csproj")))
                return directory.FullName;
        }
        throw new InvalidOperationException("Diten.ManagementGovernanceService.IntegrationTests.csproj not found above " + AppContext.BaseDirectory);
    }
}
