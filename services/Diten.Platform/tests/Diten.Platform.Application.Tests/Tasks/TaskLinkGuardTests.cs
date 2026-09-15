using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-414 — no user-facing task link is typed by hand outside <c>TaskLinks</c>.
///
/// <para><b>Why a source scan.</b> The two task addresses differ by a path segment and mean different things
/// (the Task Center detail vs the record page). Both were once hard-coded at three call sites, and every one of
/// them chose the record page — including the two that should have sent the reader to the detail. A behavioural
/// test pins a call site that exists; only a scan catches the next one somebody writes.</para>
///
/// <para><b>What is NOT a link.</b> A manifest's <c>RoutePath: "/Tasks/{id}"</c> is a route TEMPLATE (a plain
/// literal with a placeholder), not an address built for a reader, and does not match. Comment lines are skipped.
/// </para>
/// </summary>
public sealed class TaskLinkGuardTests
{
    private static readonly string[] BuilderSegments =
        ["services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Tasks", "TaskLinks.cs"];

    /// <summary>An interpolated task address, a concatenated one, or any literal naming the detail route.</summary>
    private static readonly Regex HardCodedTaskLink = new(
        @"(?:\$@?|@\$)""/(?:Tasks|WorkCenterNext/Details)/\{"
        + @"|""/(?:Tasks|WorkCenterNext/Details)/""\s*\+"
        + @"|""/WorkCenterNext/Details/",
        RegexOptions.Compiled);

    [Fact]
    public void No_task_link_is_hard_coded_outside_the_TaskLinks_builder()
    {
        var root = RepoPaths.Root();
        var builder = Path.GetFullPath(Path.Combine([root, .. BuilderSegments]));
        var files = Directory
            .EnumerateFiles(Path.Combine(root, "services", "Diten.Platform", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .ToList();

        // Non-vacuity: a scan that found no files would find no offenders.
        Assert.True(files.Count > 100, $"Only {files.Count} source files were scanned — the scan root is wrong.");
        Assert.Contains(files, path => string.Equals(Path.GetFullPath(path), builder, StringComparison.Ordinal));

        var offenders = files
            .Where(path => !string.Equals(Path.GetFullPath(path), builder, StringComparison.Ordinal))
            .SelectMany(path => CodeLines(path)
                .Where(line => HardCodedTaskLink.IsMatch(line.Text))
                .Select(line => $"{Path.GetRelativePath(root, path)}:{line.Number}: {line.Text.Trim()}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A task link is hard-coded outside TaskLinks. Use TaskLinks.Detail (where a reader is SENT to a task) or "
            + "TaskLinks.Record (the way OUT to the record page):" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_pattern_recognises_the_shapes_it_guards_and_ignores_route_templates()
    {
        // The builder itself writes both addresses — if the pattern did not see them, it would see nothing else.
        var builderMatches = CodeLines(Path.Combine([RepoPaths.Root(), .. BuilderSegments]))
            .Count(line => HardCodedTaskLink.IsMatch(line.Text));
        Assert.Equal(2, builderMatches);

        Assert.Matches(HardCodedTaskLink, "task => new RelatedRecordSummary(task.Title, $\"/Tasks/{task.Id}\"));");
        Assert.Matches(HardCodedTaskLink, "TargetUrl = \"/Tasks/\" + task.Id,");
        Assert.Matches(HardCodedTaskLink, "Link = $\"/WorkCenterNext/Details/{id}\";");
        Assert.DoesNotMatch(HardCodedTaskLink, "RoutePath: \"/Tasks/{id}\",");
        Assert.DoesNotMatch(HardCodedTaskLink, "RoutePath: \"/Tasks/WorkReport\",");
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
               || path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static IEnumerable<(int Number, string Text)> CodeLines(string path)
        => File.ReadLines(path)
            .Select((text, index) => (Number: index + 1, Text: text))
            .Where(line =>
            {
                var trimmed = line.Text.TrimStart();
                return !(trimmed.StartsWith("//", StringComparison.Ordinal)
                         || trimmed.StartsWith("*", StringComparison.Ordinal)
                         || trimmed.StartsWith("/*", StringComparison.Ordinal));
            });
}
