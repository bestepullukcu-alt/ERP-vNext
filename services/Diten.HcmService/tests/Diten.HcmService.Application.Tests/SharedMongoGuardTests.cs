using System.Text.RegularExpressions;
using Xunit;

namespace Diten.HcmService.Application.Tests;

/// <summary>
/// No test in this project names the SHARED developer mongod (owner 'Evet, düzelt' 2026-11-06). The tests start their
/// own one (TestInfrastructure/DisposableHcmMongo.cs). CI has no mongod on 27017, and locally such a URI writes into the
/// shared server. This scan reads the project's own sources, so a pasted-back URI is red here, on any machine.
/// </summary>
public sealed class SharedMongoGuardTests
{
    private static readonly Regex SharedMongo = new(@"mongodb://(localhost|127\.0\.0\.1)(:27017)?(?![0-9:])", RegexOptions.CultureInvariant);

    [Fact]
    public void No_test_names_the_shared_developer_mongod()
    {
        var project = ProjectDirectory();
        var sources = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(project, file))
            .ToArray();
        Assert.True(sources.Length >= 10, $"only {sources.Length} sources read under {project} — the guard is blind");
        var hits = sources
            .Where(file => SharedMongo.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(project, file))
            .ToArray();
        Assert.True(hits.Length == 0, "tests that name the shared mongod — use DisposableHcmMongo: " + string.Join(", ", hits));
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
            if (File.Exists(Path.Combine(directory.FullName, "Diten.HcmService.Application.Tests.csproj")))
                return directory.FullName;
        }
        throw new InvalidOperationException("Diten.HcmService.Application.Tests.csproj not found above " + AppContext.BaseDirectory);
    }
}
