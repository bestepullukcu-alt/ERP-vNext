using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial;

/// <summary>
/// The CrmService source tree for structural tests, found from the test output folder whatever <c>-o</c> points at
/// (FU-W6-S4-PATH): walking up, a folder is the service root when it holds <c>src/Diten.CrmService.Application</c>, or the
/// repository root when it holds <c>services/Diten.CrmService/src/Diten.CrmService.Application</c>.
/// </summary>
internal static class CrmSourceTree
{
    private const string Project = "Diten.CrmService.Application";

    public static string ApplicationRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, "src", Project), Path.Combine(dir.FullName, "services", "Diten.CrmService", "src", Project) })
            {
                if (File.Exists(Path.Combine(candidate, Project + ".csproj")))
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"The CrmService source tree is not above {AppContext.BaseDirectory}.");
        return string.Empty;
    }
}
