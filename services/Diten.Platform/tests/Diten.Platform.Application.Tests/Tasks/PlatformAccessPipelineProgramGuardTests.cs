using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-512 FIX1 — the cheap half of the access-pipeline guard. The BEHAVIOUR (two users, two buckets; the 31st search
/// is 429) is measured over HTTP on hosts that run <c>UsePlatformAccessPipeline</c> itself; what those tests cannot see
/// is whether Program.cs still calls it. Program.cs cannot be started in a test here (WebApplicationFactory is blocked,
/// see PlatformContainerValidationTests), so for THIS rule alone its source is read: the pipeline is called exactly
/// once, and none of its four steps is called a second time on its own (which would put them out of order).
/// </summary>
public sealed class PlatformAccessPipelineProgramGuardTests
{
    private static string ProgramSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Diten.Platform.API", "Program.cs")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Program.cs was not found above the test output folder");
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Diten.Platform.API", "Program.cs"));
        // Comments do not count as calls.
        return Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"//[^\n]*", string.Empty);
    }

    [Fact]
    public void Program_calls_the_access_pipeline_exactly_once()
    {
        Assert.Single(Regex.Matches(ProgramSource(), @"\.UsePlatformAccessPipeline\s*\("));
    }

    [Theory]
    [InlineData("UseAuthentication")]
    [InlineData("UseTenantResolution")]
    [InlineData("UseAuthorization")]
    [InlineData("UseRateLimiter")]
    public void Program_never_calls_one_of_the_four_steps_on_its_own(string step)
    {
        Assert.Empty(Regex.Matches(ProgramSource(), $@"\.{step}\s*\("));
    }
}
