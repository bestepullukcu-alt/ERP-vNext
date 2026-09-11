using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-057 at the write — measured on the PRODUCTION SOURCE, not on a copy of the rule.
///
/// <para>The behaviour tests prove the four known write paths refuse correctly. They cannot prove there is no FIFTH:
/// a new handler that writes <c>AssigneeUserId</c> is invisible to every test that does not know it exists, and
/// that is exactly how create and the recurrence rules came to write it with no check at all. This suite finds
/// every class under Features/Tasks that writes a task's or rule's assignee/pool and demands that it either asks
/// <see cref="ITaskAssignmentGuard"/> BEFORE its first write, or is on the short list of writes that are not an
/// assignment to anybody else — with the reason written down.</para>
/// </summary>
public sealed class TaskAssignmentWriteGuardSourceTests
{
    /// <summary>A write of the assignee or the pool — `x.AssigneeUserId = …` or an initializer — never `==`/`=>`, and
    /// not a parameter defaulted to null (`Guid? AssigneeUserId = null,`), which declares a filter, not a write.</summary>
    private static readonly Regex AssignmentWrite =
        new(@"(?<![\w])(?:[A-Za-z_]\w*\.)?(AssigneeUserId|PoolPositionId)\s*=(?![=>])(?!\s*null\s*[,)])", RegexOptions.Compiled);

    private static readonly Regex GuardCall = new(@"_assignmentGuard\.Check\w+Async\(", RegexOptions.Compiled);

    private static readonly Regex PersistCall =
        new(@"\b_(tasks|rules)\.(CreateAsync|UpdateAsync)\(", RegexOptions.Compiled);

    /// <summary>Writes that hand work to a caller-chosen person or pool. Each must ask the guard first.</summary>
    private static readonly string[] Guarded =
    [
        nameof(CreateTaskItemHandler),
        nameof(ReassignTaskItemHandler),
        nameof(CreateTaskRecurrenceRuleHandler),
        nameof(UpdateTaskRecurrenceRuleHandler)
    ];

    /// <summary>Writes that name nobody the caller chose. Adding to this list is a design decision, not a fix.</summary>
    private static readonly IReadOnlyDictionary<string, string> NotAnAssignment = new Dictionary<string, string>
    {
        [nameof(ClaimTaskItemHandler)] = "the caller takes pooled work for THEMSELVES",
        [nameof(ReleaseTaskItemHandler)] = "the holder is cleared back to the pool; nobody is named",
        [nameof(ReturnTaskItemHandler)] = "work goes back to its REQUESTER, fixed by the task, not chosen"
    };

    [Fact]
    public void Every_class_that_writes_an_assignee_or_pool_is_either_guarded_or_explicitly_not_an_assignment()
    {
        var writers = WritersUnder(TasksRoot()).Keys.ToHashSet();
        var classified = Guarded.Concat(NotAnAssignment.Keys).ToHashSet();

        var unclassified = writers.Except(classified).OrderBy(x => x).ToList();
        Assert.True(unclassified.Count == 0,
            "These classes write AssigneeUserId/PoolPositionId but are neither guarded nor classified: "
            + string.Join(", ", unclassified)
            + ". Route them through ITaskAssignmentGuard, or add them to NotAnAssignment WITH a reason.");

        // A stale entry would make the list lie about what it covers.
        var stale = classified.Except(writers).OrderBy(x => x).ToList();
        Assert.True(stale.Count == 0, "Classified but no longer writing an assignee/pool: " + string.Join(", ", stale));
    }

    [Fact]
    public void Every_guarded_write_asks_the_guard_BEFORE_it_persists_anything()
    {
        var writers = WritersUnder(TasksRoot());
        var failures = new List<string>();

        foreach (var name in Guarded)
        {
            var body = writers[name];
            var guard = GuardCall.Match(body);
            var persist = PersistCall.Match(body);

            if (!guard.Success)
            {
                failures.Add($"{name}: never calls _assignmentGuard.Check…Async");
            }
            else if (!persist.Success)
            {
                failures.Add($"{name}: no _tasks/_rules write found — the ordering check measures nothing");
            }
            else if (guard.Index > persist.Index)
            {
                failures.Add($"{name}: persists before asking the guard");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Every_guarded_handler_REQUIRES_the_guard()
    {
        // Optional would mean "a missing registration silently admits everybody". Required fails at startup.
        foreach (var name in Guarded)
        {
            var type = typeof(CreateTaskItemHandler).Assembly.GetTypes().Single(t => t.Name == name);
            var parameter = Assert.Single(
                type.GetConstructors().Single().GetParameters(),
                p => p.ParameterType == typeof(ITaskAssignmentGuard));
            Assert.False(parameter.IsOptional, $"{name} takes the assignment guard as OPTIONAL");
        }
    }

    [Fact]
    public void The_pickers_and_the_guard_ask_the_SAME_rule_and_nobody_keeps_a_copy()
    {
        var root = TasksRoot();
        string Source(string file) => File.ReadAllText(
            Directory.GetFiles(root, file, SearchOption.AllDirectories).Single());

        foreach (var file in new[]
                 {
                     "GetTaskAssignmentPersonLookupHandler.cs",
                     "GetTaskAssignmentPositionLookupHandler.cs",
                     "TaskAssignmentGuard.cs"
                 })
        {
            Assert.Contains("TaskAssigneeEligibility.Judge(", Source(file));
        }

        // The copy that had already drifted: the person picker tested the position status itself.
        foreach (var picker in new[] { "GetTaskAssignmentPersonLookupHandler.cs", "GetTaskAssignmentPositionLookupHandler.cs" })
        {
            Assert.DoesNotContain("PositionStatus.Active", Code(Source(picker)));
            Assert.DoesNotContain(".Allows(", Code(Source(picker)));
        }

        // Allows() is asked in exactly one place outside its own definition.
        var askers = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => Code(File.ReadAllText(f)).Contains(".Allows("))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Equal(["TaskAssigneeEligibility.cs"], askers);
    }

    [Fact]
    public void Only_the_recurrence_sweep_may_declare_itself_callerless()
    {
        /*
         * IsScheduledGeneration switches the guard off. It lives on the COMMAND so no request body can set it; this
         * pins that only the sweep sets it, and that the template path merely passes it through.
         */
        var application = Path.GetFullPath(Path.Combine(TasksRoot(), "..", ".."));
        var api = Path.GetFullPath(Path.Combine(application, "..", "Diten.Platform.API"));

        var setters = new[] { application, api }
            .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(f => Regex.IsMatch(Code(File.ReadAllText(f)), @"IsScheduledGeneration\s*[:=]\s*true"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Equal(["GenerateDueRecurringTasksHandler.cs"], setters);

        var mentions = new[] { application, api }
            .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(f => Code(File.ReadAllText(f)).Contains("IsScheduledGeneration"))
            .Select(Path.GetFileName)
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(
            [
                "CreateTaskItemFromTemplateHandler.cs",
                "CreateTaskItemHandler.cs",
                "GenerateDueRecurringTasksHandler.cs",
                "TaskItemCommands.cs"
            ],
            mentions);
    }

    // ── source helpers ───────────────────────────────────────────────────────

    /// <summary>Class name → its code (comments stripped), for every class under Features/Tasks that writes an
    /// assignee or pool.</summary>
    private static Dictionary<string, string> WritersUnder(string root)
    {
        var writers = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            foreach (var (name, body) in Classes(Code(File.ReadAllText(file))))
            {
                if (AssignmentWrite.IsMatch(body)) { writers[name] = body; }
            }
        }

        return writers;
    }

    private static IEnumerable<(string Name, string Body)> Classes(string code)
    {
        var heads = Regex.Matches(code, @"\b(?:class|record)\s+(\w+)");
        for (var i = 0; i < heads.Count; i++)
        {
            var end = i + 1 < heads.Count ? heads[i + 1].Index : code.Length;
            yield return (heads[i].Groups[1].Value, code[heads[i].Index..end]);
        }
    }

    /// <summary>The source with comments removed, so a comment that MENTIONS a call cannot satisfy the guard.</summary>
    private static string Code(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//[^\n]*", string.Empty);
    }

    private static string TasksRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src", "Diten.Platform.Application", "Features", "Tasks");
    }
}
