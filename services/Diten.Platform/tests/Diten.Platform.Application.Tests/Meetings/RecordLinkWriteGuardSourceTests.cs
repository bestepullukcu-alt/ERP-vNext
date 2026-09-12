using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 pack §3 / K1, measured on the PRODUCTION SOURCE, not on a copy of the rule — the same discipline
/// `TaskAssignmentWriteGuardSourceTests` already established for BL-057.
///
/// <para><b>Why a source scan and not only a behaviour test.</b> A behaviour test proves the write paths this
/// suite KNOWS about are correct. It cannot prove there is no fifth path: a new class that starts writing
/// <c>RecordLink</c> directly is invisible to every test that does not know it exists — the exact shape of how
/// the BL-057 assignment guard was once bypassed.</para>
/// </summary>
public sealed class RecordLinkWriteGuardSourceTests
{
    /// <summary>The one class this pack names as the ONLY writer in this slice (pack §3, "MOD-0357 is the
    /// first owner and — in this slice — the ONLY writer").</summary>
    private const string OnlyWriter = "RecordLinkService";

    /// <summary>The repository's own write methods, called through ITS OWN conventional field name (`_links`,
    /// in both <c>RecordLinkRepository</c> and <c>RecordLinkService</c>).</summary>
    private static readonly Regex RepositoryWriteCall =
        new(@"_links\.(CreateAsync|FindOrCreateAsync|DeleteAsync)\(", RegexOptions.Compiled);

    [Fact]
    public void Only_RecordLinkService_calls_IRecordLinkRepositorys_write_methods()
    {
        // Scoped to files that reference IRecordLinkRepository AT ALL: `_links` is a field name other,
        // unrelated services in this codebase happen to reuse (measured — ExternalDocumentRegisterService is
        // one), so the write-call pattern alone would false-positive on a coincidence. Requiring the actual
        // repository TYPE in the same file is what makes this a RecordLink-specific check.
        var writers = Directory.GetFiles(ApplicationRoot(), "*.cs", SearchOption.AllDirectories)
            .Select(file => (File: file, Body: Code(File.ReadAllText(file))))
            .Where(x => Regex.IsMatch(x.Body, @"IRecordLinkRepository\b") && RepositoryWriteCall.IsMatch(x.Body))
            .Select(x => Path.GetFileNameWithoutExtension(x.File))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.Equal([OnlyWriter], writers);
    }

    /// <summary>
    /// K1's own boundary, as a fact about the CODE: MOD-0024 never writes a <c>RecordLink</c> row. This is the
    /// WP's own AC7 wording — "Features/Tasks/** altında hiçbir dosya IRecordLinkService/IRecordLinkRepository
    /// YAZMA metodunu çağırmaz" — checked by TYPE, not by field name: every file under <c>Features/Tasks</c>
    /// that references <c>IRecordLinkService</c> at all (today: only <c>TaskWorkItemProvider</c>, for the
    /// READ-side `relatedRecords` projection) must not call either of that interface's two write methods.
    /// <c>IRecordLinkRepository</c> is not referenced anywhere under <c>Features/Tasks</c> at all — the
    /// feature only ever sees the SERVICE, never the raw repository.
    /// </summary>
    [Fact]
    public void Nothing_under_Features_Tasks_calls_a_RecordLink_WRITE_method()
    {
        var tasksRoot = Path.Combine(ApplicationRoot(), "Tasks");
        Assert.True(Directory.Exists(tasksRoot), $"Expected Features/Tasks under {ApplicationRoot()}.");

        var repositoryReferences = Directory.GetFiles(tasksRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => Regex.IsMatch(Code(File.ReadAllText(file)), @"IRecordLinkRepository\b"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(repositoryReferences.Count == 0,
            "Features/Tasks must consume the RecordLink bridge through IRecordLinkService only, never the raw "
            + "repository: " + string.Join(", ", repositoryReferences));

        var writeCallers = Directory.GetFiles(tasksRoot, "*.cs", SearchOption.AllDirectories)
            .Select(file => (File: file, Body: Code(File.ReadAllText(file))))
            .Where(x => Regex.IsMatch(x.Body, @"IRecordLinkService\b")
                        && Regex.IsMatch(x.Body, @"\.(AddLinkAsync|RemoveLinkAsync)\s*\("))
            .Select(x => Path.GetFileName(x.File))
            .ToList();
        Assert.True(writeCallers.Count == 0,
            "Features/Tasks must never call IRecordLinkService's write methods (AddLinkAsync/RemoveLinkAsync): "
            + string.Join(", ", writeCallers));
    }

    /// <summary>Non-vacuity for the test above: it must actually be looking at a file, not passing because it
    /// found nothing to check at all.</summary>
    [Fact]
    public void The_read_side_really_is_present_under_Features_Tasks_so_the_guard_above_is_not_vacuous()
    {
        var tasksRoot = Path.Combine(ApplicationRoot(), "Tasks");
        var readers = Directory.GetFiles(tasksRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => Regex.IsMatch(Code(File.ReadAllText(file)), @"IRecordLinkService\b"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Contains("TaskWorkItemProvider.cs", readers);
    }

    // ── source helpers ───────────────────────────────────────────────────────

    /// <summary>The source with comments stripped, so a comment that MENTIONS a call cannot satisfy/trip a
    /// regex meant to see real code.</summary>
    private static string Code(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//[^\n]*", string.Empty);
    }

    private static string ApplicationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src", "Diten.Platform.Application", "Features");
    }
}
