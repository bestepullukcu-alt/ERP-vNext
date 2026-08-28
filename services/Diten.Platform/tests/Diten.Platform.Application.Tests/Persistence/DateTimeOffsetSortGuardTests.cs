using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

// Guard against the regression that killed the MOD-0023 transition gate.
//
// No DateTimeOffsetSerializer is registered anywhere (BL-030), so the Mongo driver stores every
// DateTimeOffset as a BSON array [ticks, offsetMinutes]. A server-side sort whose keys include TWO such
// fields is rejected at runtime with "cannot sort with keys that are parallel arrays" — it compiles, it
// passes every fake-repository test, and it fails only against a real database.
//
// This test scans the actual repository sources across all services and fails on any
// .SortBy*(...).ThenBy*(...) chain with two or more DateTimeOffset keys. When BL-030 lands and a global
// serializer is registered, this guard becomes unnecessary and should be removed together with the
// in-memory ordering it protects.
public sealed class DateTimeOffsetSortGuardTests
{
    // .SortBy(x => x.A).ThenByDescending(x => x.B)... — the driver's fluent sort chain, in one match.
    private static readonly Regex SortChain = new(
        @"\.SortBy(?:Descending)?\s*\(\s*\w+\s*=>\s*\w+\.(?<first>\w+)\s*\)(?<rest>(?:\s*\.ThenBy(?:Descending)?\s*\(\s*\w+\s*=>\s*\w+\.\w+\s*\))+)",
        RegexOptions.Compiled);

    private static readonly Regex ThenByKey = new(
        @"\.ThenBy(?:Descending)?\s*\(\s*\w+\s*=>\s*\w+\.(?<key>\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex DateTimeOffsetProperty = new(
        @"\b(?:public|internal|protected)\s+(?:virtual\s+|required\s+|override\s+)*DateTimeOffset\??\s+(?<name>\w+)\s*(?:\{|=>)",
        RegexOptions.Compiled);

    [Fact]
    public void No_repository_sorts_on_two_date_time_offset_keys()
    {
        var servicesRoot = LocateServicesRoot();
        var sourceFiles = Directory
            .EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        // Property harvesting reads everything; the chain scan below is production code only. Test code is
        // exempt on purpose: WorkflowInstanceLookupMongoTests issues the forbidden two-key sort deliberately,
        // to assert the server still rejects it and thus that the in-memory ordering is still required.
        var productionFiles = sourceFiles
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(sourceFiles);

        var dateTimeOffsetProperties = CollectDateTimeOffsetPropertyNames(sourceFiles);

        // Sanity check: if the property harvest silently found nothing, the guard below would pass
        // vacuously — exactly the failure mode this whole slice is about.
        Assert.Contains("CreatedAt", dateTimeOffsetProperties);
        Assert.Contains("UpdatedAt", dateTimeOffsetProperties);
        Assert.Contains("StartedAt", dateTimeOffsetProperties);

        var violations = new List<string>();

        Assert.NotEmpty(productionFiles);

        foreach (var path in productionFiles)
        {
            var source = StripFullLineComments(File.ReadAllText(path));

            foreach (Match chain in SortChain.Matches(source))
            {
                var keys = new List<string> { chain.Groups["first"].Value };
                keys.AddRange(ThenByKey.Matches(chain.Groups["rest"].Value).Select(m => m.Groups["key"].Value));

                var dateKeys = keys.Where(dateTimeOffsetProperties.Contains).ToList();
                if (dateKeys.Count >= 2)
                {
                    violations.Add($"{Path.GetRelativePath(servicesRoot, path)}: sorts on {string.Join(" + ", dateKeys)}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "MongoDB cannot sort on two DateTimeOffset keys while BL-030 is open — every DateTimeOffset is "
            + "stored as a BSON array and the server rejects the query with \"cannot sort with keys that are "
            + "parallel arrays\". Order these results in memory instead (see "
            + "WorkflowInstanceRepository.GetLatestByObjectRefAsync):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static HashSet<string> CollectDateTimeOffsetPropertyNames(IEnumerable<string> sourceFiles)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in sourceFiles)
        {
            foreach (Match match in DateTimeOffsetProperty.Matches(File.ReadAllText(path)))
            {
                names.Add(match.Groups["name"].Value);
            }
        }

        return names;
    }

    // Only whole-line comments are removed, so that a `//` inside a string literal (connection strings)
    // is left alone. The comments that document this very rule quote the forbidden sort chain verbatim
    // and would otherwise report themselves as violations.
    private static string StripFullLineComments(string source)
    {
        var builder = new StringBuilder(source.Length);
        foreach (var line in source.Split('\n'))
        {
            if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                builder.Append(line).Append('\n');
            }
        }

        return builder.ToString();
    }

    // Walked up to the AGENTS.md marker, not to a `.git` DIRECTORY: in a git worktree `.git` is a FILE, so
    // the old check never matched and this threw instead of finding the root. See RepoPaths.
    private static string LocateServicesRoot()
        => RepoPaths.Services();
}
