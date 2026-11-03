using System.Text;
using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE SECOND GUARD — A WORKFLOW RECORD IS NEVER DELETED, WHEREVER THE TEXT IS (MOD-0023 §17, AC-W15, DL-2).
 *
 * WHY A SECOND FILE. The first guard (WorkflowRetentionGuardTests) holds the three repositories, their interfaces, the manifest
 * and Platform.Common. DL-1's verification found what it does not read: the fourth store (WorkflowStartRecoveryStore) body,
 * and any marker write placed under Features/Workflow. Its helpers are private and its file is frozen, so the helpers this
 * guard needs are copied here and the rules below are new. Owner decision S17-8 still stands: the fourth store gets no
 * DeleteAsync override; this guard keeps that path shut by reading text.
 *
 * THE RULES (pure functions: text in, findings out; a sabotage hands them another text, never a protected file).
 *   (1) WorkflowStartRecoveryStore: no delete call, no DeleteAsync call, no deletion-marker write in its file.
 *   (2) The scan set: every .cs under Features/Workflow, and every .cs under Infrastructure that names WorkflowInstance,
 *       ApprovalTask or WorkflowTransitionLog, or PlatformCollections.WorkflowInstances / ApprovalTasks /
 *       WorkflowTransitionLogs, as a whole word after comments and strings are removed (WorkflowInstanceStatus and
 *       ApprovalTaskId do not match). In that set no DeleteOne, DeleteMany, FindOneAndDelete, DeleteOneModel,
 *       DeleteManyModel or `base.DeleteAsync`.
 *   (3) In that set no deletion-marker write: `.Set(x => x.IsDeleted`, `.Set(x => x.DeletedAt`, `Set("IsDeleted"`,
 *       `Set("DeletedAt"`, `Set(nameof(….IsDeleted))`, `Set(nameof(….DeletedAt))`, `new BsonDocument("IsDeleted", true)`,
 *       `IsDeleted = true`, `DeletedAt =` anything but null. The string forms are read on a second text that keeps string
 *       contents (the first removes them).
 *   (4) A `.Set(…)` marker counts only when the nearest preceding `Builders<T>` names one of the three types or BsonDocument;
 *       another type does not (two legitimate writes exist today: an SLA rule and a tenant domain). With no preceding
 *       `Builders<T>` at all it counts (fail closed).
 *   (5) A declaration is not a finding (`bool IsDeleted = false` on a record, `DateTimeOffset? DeletedAt = null`), nor is a
 *       read (`Filter.Eq(x => x.IsDeleted, false)`, `.Ascending(x => x.IsDeleted)`, `x.IsDeleted`).
 *   (6) An in-place alias update is allowed (§17.8/4): `.Set(x => x.ActorId, …)`, `UpdateOneAsync`, `ReplaceOneAsync` give
 *       no finding; the positive controls below pin that.
 *   (7) Under Infrastructure, every class whose base list takes one of the three types as a generic argument, and every open
 *       generic `class X<T> : TenantRepository<T> / RepositoryBase<T> / HybridRepository<T> / GlobalRepository<T>`, is
 *       listed. The expected set is the four classes of the first guard; a fifth (an intermediate base included) is red.
 *
 * NOT COVERED, STATED (this guard reads text, not a syntax tree):
 *   • a using alias for a retained type or for a base type;
 *   • reflection, or a delete reached through a dynamic or generic helper;
 *   • a bare `GetCollection<BsonDocument>("workflow_instances")` (or the other two names) in a file that names none of the
 *     types or PlatformCollections members;
 *   • a `Builders<T>` held in a variable, so that another `Builders<Other>` sits between it and the `.Set(…)` and the type
 *     filter reads the wrong type;
 *   • Application writes outside Features/Workflow (Features/Tasks, TimeEntry and WorkAggregation are not scanned).
 * The behaviour tests over the real repositories cover the override itself.
 *
 * SABOTAGE. The real-source facts read their text through FourthStoreSource, ScanSetSources and InfrastructureSources; a
 * sabotage appends text at the line tagged SAB-… in that method and runs the one fact named in the recipe.
 */
public class WorkflowRetentionSourceGuardTests
{
    private const string RecoveryStorePath =
        "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowStartRecoveryStore.cs";
    private const string WorkflowFeatureRoot = "services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow";
    private const string InfrastructureRoot = "services/Diten.Platform/src/Diten.Platform.Infrastructure";
    private const string RecoveryStore = "WorkflowStartRecoveryStore";

    private static readonly string[] ExpectedBaseClasses =
        ["ApprovalTaskRepository", "WorkflowInstanceRepository", "WorkflowStartRecoveryStore", "WorkflowTransitionLogRepository"];

    private static readonly string[] ExpectedScanSetMembers =
    [
        "WorkflowRepositories.cs",
        "WorkflowStartRecoveryStore.cs",
        "PlatformSchemaManifest.WorkflowWorkCenter.cs",
        "PlatformSchemaMigrations.cs",
        "WorkflowInstanceBsonMap.cs"
    ];

    private static readonly Regex DeletingCall = new(
        @"\b(?:DeleteOne|DeleteMany|FindOneAndDelete)(?:Async|Model)?\b|\bbase\s*\.\s*DeleteAsync\b", RegexOptions.Compiled);

    private static readonly Regex DeleteAsyncCall = new(@"\bDeleteAsync\s*\(", RegexOptions.Compiled);

    private static readonly Regex NamesRetainedType = new(
        @"\b(?:WorkflowInstance|ApprovalTask|WorkflowTransitionLog)\b|\bPlatformCollections\s*\.\s*(?:WorkflowInstances|ApprovalTasks|WorkflowTransitionLogs)\b",
        RegexOptions.Compiled);

    // `.Set(x => x.IsDeleted`, `.Set((x) => x.DeletedAt` — a lambda on the marker, read on the text without strings.
    private static readonly Regex SetLambdaMarker = new(
        @"\.\s*Set\s*\(\s*(?:\w+|\(\s*\w+\s*\))\s*=>\s*[\w.]*?\b(?:IsDeleted|DeletedAt)\b", RegexOptions.Compiled);

    private static readonly Regex SetNameofMarker = new(
        @"\.\s*Set\s*\(\s*nameof\s*\([^()]*\b(?:IsDeleted|DeletedAt)\s*\)", RegexOptions.Compiled);

    // `.Set("IsDeleted"` — read on the text that keeps strings.
    private static readonly Regex SetStringMarker = new(
        @"\.\s*Set\s*\(\s*""(?:IsDeleted|DeletedAt)""", RegexOptions.Compiled);

    private static readonly Regex BsonDocumentMarker = new(
        @"\bnew\s+BsonDocument\s*\(\s*""(?:IsDeleted|DeletedAt)""\s*,\s*true\b", RegexOptions.Compiled);

    private static readonly Regex AssignedDeleted = new(@"\bIsDeleted\s*=(?![=>])\s*true\b", RegexOptions.Compiled);

    private static readonly Regex AssignedDeletedAt = new(@"\bDeletedAt\s*=(?![=>])(?!\s*null\b)", RegexOptions.Compiled);

    private static readonly Regex BuildersOf = new(@"\bBuilders\s*<\s*(?<type>[\w.]+)\s*>", RegexOptions.Compiled);

    private static readonly Regex ClassHeader = new(@"\bclass\s+(?<name>\w+)", RegexOptions.Compiled);

    private static readonly Regex WhereClause = new(@"\bwhere\b", RegexOptions.Compiled);

    private static readonly Regex RetainedGenericArgument = new(
        @"<[^<>]*\b(?:WorkflowInstance|ApprovalTask|WorkflowTransitionLog)\b[^<>]*>", RegexOptions.Compiled);

    private static readonly Regex OpenGenericBase = new(
        @"\b(?:TenantRepository|RepositoryBase|HybridRepository|GlobalRepository)\s*<\s*(?<arg>\w+)\s*>", RegexOptions.Compiled);

    private static readonly HashSet<string> MarkerTypes =
        new(["WorkflowInstance", "ApprovalTask", "WorkflowTransitionLog", "BsonDocument"], StringComparer.Ordinal);

    // ─────────────────────────────────────────────────────────── the rules, over the real sources

    [Fact]
    public void AC_W15_the_fourth_store_deletes_nothing_and_writes_no_deletion_marker()
    {
        var findings = FourthStoreFindings(FourthStoreSource()).ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AC_W15_the_scan_set_is_not_blind()
    {
        var members = ScanSetPaths(ScanSetSources()).Select(path => path[(path.LastIndexOf('/') + 1)..]).ToArray();
        foreach (var expected in ExpectedScanSetMembers)
        {
            Assert.Contains(expected, members);
        }

        Assert.True(
            ScanSetPaths(ScanSetSources()).Count(path => path.StartsWith(WorkflowFeatureRoot, StringComparison.Ordinal)) >= 50,
            "fewer than 50 files under Features/Workflow reached the scan set — the scan is blind");
    }

    [Fact]
    public void AC_W15_no_file_in_the_scan_set_deletes_a_row_or_writes_a_deletion_marker()
    {
        var findings = ScanSetFindings(ScanSetSources()).ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AC_W15_exactly_the_four_expected_classes_take_a_retained_type_as_a_generic_base_argument()
    {
        var found = BaseClassesOfRetainedTypes(InfrastructureSources()).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(ExpectedBaseClasses, found);
    }

    // ─────────────────────────────────────────────────────────── the rules have teeth (a text, never a protected file)

    public static IEnumerable<object[]> StoreSabotage() =>
    [
        // G1 — a delete call in the fourth store body
        ["""
            public sealed class WorkflowStartRecoveryStore : TenantRepository<WorkflowInstance>, IWorkflowStartRecoveryStore
            {
                public Task Wipe(FilterDefinition<WorkflowInstance> f) => Collection.DeleteManyAsync(f);
            }
            """],
        // G2 — a marker write in the fourth store
        ["""
            public sealed class WorkflowStartRecoveryStore : TenantRepository<WorkflowInstance>, IWorkflowStartRecoveryStore
            {
                public Task Mark(FilterDefinition<WorkflowInstance> f) =>
                    Collection.UpdateOneAsync(f, Builders<WorkflowInstance>.Update.Set(x => x.IsDeleted, true));
            }
            """],
        // the inherited delete reached through the base class
        ["""
            public sealed class WorkflowStartRecoveryStore : TenantRepository<WorkflowInstance>, IWorkflowStartRecoveryStore
            {
                public Task Remove(Guid id) => DeleteAsync(id);
            }
            """]
    ];

    [Theory]
    [MemberData(nameof(StoreSabotage))]
    public void AC_W15_G1_G2_a_delete_or_a_marker_in_the_fourth_store_is_red(string source) =>
        Assert.NotEmpty(FourthStoreFindings(source));

    [Fact]
    public void AC_W15_a_fourth_store_that_only_reads_and_updates_in_place_is_green() =>
        Assert.Empty(FourthStoreFindings("""
            public sealed class WorkflowStartRecoveryStore : TenantRepository<WorkflowInstance>, IWorkflowStartRecoveryStore
            {
                // Comments do not count: Collection.DeleteManyAsync(f); IsDeleted = true;
                public Task<long> Count(FilterDefinition<WorkflowInstance> f) => Collection.CountDocumentsAsync(f);
                public Task Rename(FilterDefinition<WorkflowInstance> f) =>
                    Collection.UpdateOneAsync(f, Builders<WorkflowInstance>.Update.Set(x => x.StartedBy, "pseudonym"));
                private const string Message = "DeleteManyAsync IsDeleted = true";
            }
            """));

    [Fact]
    public void AC_W15_a_missing_fourth_store_class_is_red_not_green() =>
        Assert.NotEmpty(FourthStoreFindings("public sealed class Other { }"));

    public static IEnumerable<object[]> ScanSabotage() =>
    [
        // G3 — an assignment of the marker under Features/Workflow
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { void M(WorkflowInstance instance) { instance.IsDeleted = true; } }"],
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { void M(ApprovalTask task) { task.DeletedAt = DateTimeOffset.UtcNow; } }"],
        // G4 — a string-named marker on a BsonDocument builder
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", """class Sab { void M() { var u = Builders<BsonDocument>.Update.Set("IsDeleted", true); } }"""],
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", """class Sab { void M() { var d = new BsonDocument("IsDeleted", true); } }"""],
        // G5 — a nameof marker on a retained type builder
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { void M() { var u = Builders<WorkflowInstance>.Update.Set(nameof(WorkflowInstance.IsDeleted), true); } }"],
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { void M() { var u = Builders<WorkflowInstance>.Update.Set(x => x.IsDeleted, true); } }"],
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { void M() { var u = Builders<WorkflowTransitionLog>.Update.Set(x => x.DeletedAt, now); } }"],
        // G6 — a bulk delete model
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { Task M() => c.BulkWriteAsync([new DeleteOneModel<ApprovalTask>(f)]); }"],
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { Task M() => c.FindOneAndDeleteAsync(f); }"],
        [WorkflowFeatureRoot + "/Handlers/CommandHandlers/Sab.cs", "class Sab { Task M() => base.DeleteAsync(id); }"],
        // G8 — a file under Infrastructure that names only a PlatformCollections member
        [InfrastructureRoot + "/Persistence/Sab.cs", "class Sab { string C = PlatformCollections.ApprovalTasks; Task M(IMongoCollection<BsonDocument> c) => c.DeleteManyAsync(f); }"],
        [InfrastructureRoot + "/Persistence/Sab.cs", "class Sab { Task M(IMongoCollection<WorkflowInstance> c) => c.DeleteOneAsync(f); }"]
    ];

    [Theory]
    [MemberData(nameof(ScanSabotage))]
    public void AC_W15_G3_to_G6_G8_a_delete_or_a_marker_in_the_scan_set_is_red(string path, string source) =>
        Assert.NotEmpty(ScanSetFindings([(path, source)]));

    public static IEnumerable<object[]> PositiveControls() =>
    [
        // an in-place alias update is allowed (§17.8/4)
        [WorkflowFeatureRoot + "/X.cs", "class X { void M() { var u = Builders<ApprovalTask>.Update.Set(x => x.ActorId, p); } }"],
        [WorkflowFeatureRoot + "/X.cs", "class X { void M() { var u = Builders<WorkflowInstance>.Update.Set(x => x.StartedBy, p); } }"],
        [WorkflowFeatureRoot + "/X.cs", "class X { Task M() => c.UpdateOneAsync(f, u); Task N() => c.ReplaceOneAsync(f, d); }"],
        // a declaration is not a write
        [WorkflowFeatureRoot + "/X.cs", "public sealed record R(Guid Id, bool IsDeleted = false);"],
        [WorkflowFeatureRoot + "/X.cs", "public sealed record R(Guid Id, DateTimeOffset? DeletedAt = null);"],
        // a read is not a write
        [WorkflowFeatureRoot + "/X.cs", "class X { void M() { var f = Builders<ApprovalTask>.Filter.Eq(x => x.IsDeleted, false); } }"],
        [WorkflowFeatureRoot + "/X.cs", "class X { void M() { var k = Builders<ApprovalTask>.IndexKeys.Ascending(x => x.IsDeleted); var d = x.IsDeleted; } }"],
        [WorkflowFeatureRoot + "/X.cs", "class X { bool M(ApprovalTask t) => t.IsDeleted == false && t.DeletedAt is null; }"],
        // another type's marker write is legitimate (SLA rule, tenant domain)
        [WorkflowFeatureRoot + "/X.cs", "class X { void M() { var u = Builders<SlaEscalationRule>.Update.Set(x => x.IsDeleted, true); } }"],
        [InfrastructureRoot + "/Persistence/Schema/PlatformSchemaMigrations.cs", "class X { WorkflowInstance w; void M() { var u = Builders<TenantDomain>.Update.Set(x => x.IsDeleted, true).Set(x => x.UpdatedAt, now); } }"],
        // comments and strings carry no finding
        [WorkflowFeatureRoot + "/X.cs", "class X { // task.IsDeleted = true; c.DeleteManyAsync(f);\n string s = \"IsDeleted = true; DeleteOneAsync\"; /* new BsonDocument(\"IsDeleted\", true) */ }"],
        // a file under Infrastructure that names no retained type is outside the scan set (WorkflowInstanceStatus, ApprovalTaskId are not the types)
        [InfrastructureRoot + "/Persistence/Y.cs", "class Y { WorkflowInstanceStatus s; Guid ApprovalTaskId; Task M(IMongoCollection<Tenant> c) => c.DeleteManyAsync(f); }"]
    ];

    [Theory]
    [MemberData(nameof(PositiveControls))]
    public void AC_W15_positive_control_in_place_updates_declarations_reads_and_other_types_are_green(string path, string source) =>
        Assert.Empty(ScanSetFindings([(path, source)]));

    [Fact]
    public void AC_W15_the_type_filter_reads_the_nearest_preceding_Builders_type()
    {
        // The nearest preceding builder is another type: not a finding (stated escape, the filter is by position).
        Assert.Empty(ScanSetFindings([(WorkflowFeatureRoot + "/X.cs",
            "class X { void M() { var a = Builders<ApprovalTask>.Filter.Empty; var u = Builders<SlaEscalationRule>.Update.Set(x => x.IsDeleted, true); } }")]));
        // A retained-type builder after it is a finding.
        Assert.NotEmpty(ScanSetFindings([(WorkflowFeatureRoot + "/X.cs",
            "class X { void M() { var a = Builders<SlaEscalationRule>.Filter.Empty; var u = Builders<ApprovalTask>.Update.Set(x => x.IsDeleted, true); } }")]));
        // No builder at all before the write: fail closed.
        Assert.NotEmpty(ScanSetFindings([(WorkflowFeatureRoot + "/X.cs", "class X { void M(UpdateDefinition<object> u) { u.Set(x => x.IsDeleted, true); } }")]));
    }

    public static IEnumerable<object[]> BaseSabotage() =>
    [
        // G7 — an intermediate open generic base and a class on top of it
        ["""
            public abstract class Mid<T> : TenantRepository<T> where T : class { }
            public sealed class X : Mid<ApprovalTask> { }
            """,
            "Mid,X"],
        ["public sealed class Extra : TenantRepository<WorkflowTransitionLog>, IExtra { }", "Extra"],
        ["public sealed class Extra(IPlatformDbContext db)\n    : HybridRepository<Diten.Platform.Domain.Entities.Workflow.WorkflowInstance>(db) { }", "Extra"],
        ["public abstract class Open<TEntity> : GlobalRepository<TEntity> { }", "Open"],
        ["public abstract class Open2<TEntity> : RepositoryBase<TEntity> where TEntity : class { }", "Open2"]
    ];

    [Theory]
    [MemberData(nameof(BaseSabotage))]
    public void AC_W15_G7_a_class_taking_a_retained_type_or_an_open_generic_repository_base_is_listed(string source, string names) =>
        Assert.Equal(names.Split(','), BaseClassesOfRetainedTypes([source]).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void AC_W15_classes_with_another_base_or_no_base_are_not_listed() =>
        Assert.Empty(BaseClassesOfRetainedTypes(
        [
            "public sealed class Rules : TenantRepository<SlaEscalationRule>, ISlaEscalationRuleRepository { }",
            "public abstract class RepositoryBase<TEntity>\n    where TEntity : BaseEntity { }",
            "public sealed class SchemaCollection<TDocument> : SchemaCollection { }",
            "public static class WorkflowInstanceBsonMap { }",
            "// public sealed class Extra : TenantRepository<ApprovalTask> { }"
        ]));

    // ─────────────────────────────────────────────────────────── the checks (pure: text in, findings out)

    private static IEnumerable<string> FourthStoreFindings(string source)
    {
        var body = ClassBody(Scrub(source), "class", RecoveryStore);
        if (body is null)
        {
            yield return $"{RecoveryStore}: class not found — the scan is blind.";
            yield break;
        }

        if (DeletingCall.Match(body) is { Success: true } call)
        {
            yield return $"{RecoveryStore}: deletes rows ({call.Value}).";
        }

        if (DeleteAsyncCall.Match(body) is { Success: true })
        {
            yield return $"{RecoveryStore}: calls DeleteAsync — that store keeps the inherited delete (S17-8), nothing may call it.";
        }

        foreach (var marker in MarkerFindings(source))
        {
            yield return $"{RecoveryStore}: {marker}";
        }
    }

    private static IEnumerable<string> ScanSetFindings(IEnumerable<(string Path, string Source)> files)
    {
        foreach (var file in files)
        {
            var text = Scrub(file.Source);
            if (!InScanSet(file.Path, text))
            {
                continue;
            }

            if (DeletingCall.Match(text) is { Success: true } call)
            {
                yield return $"{file.Path}: deletes rows ({call.Value}).";
            }

            foreach (var marker in MarkerFindings(file.Source))
            {
                yield return $"{file.Path}: {marker}";
            }
        }
    }

    private static IReadOnlyList<string> ScanSetPaths(IEnumerable<(string Path, string Source)> files) =>
        files.Where(file => InScanSet(file.Path, Scrub(file.Source))).Select(file => file.Path).ToArray();

    private static bool InScanSet(string path, string scrubbed) =>
        path.StartsWith(WorkflowFeatureRoot, StringComparison.Ordinal)
        || (path.StartsWith(InfrastructureRoot, StringComparison.Ordinal) && NamesRetainedType.IsMatch(scrubbed));

    /// <summary>The marker writes in a text: the lambda and nameof forms on the text without strings, the string forms on
    /// the text that keeps them. A `.Set(…)` counts by the nearest preceding Builders&lt;T&gt; (rule 4).</summary>
    private static IEnumerable<string> MarkerFindings(string source)
    {
        var plain = Scrub(source);
        var withStrings = Scrub(source, keepStrings: true);

        foreach (var (text, regex) in new[] { (plain, SetLambdaMarker), (plain, SetNameofMarker), (withStrings, SetStringMarker) })
        {
            foreach (Match match in regex.Matches(text))
            {
                var type = NearestBuilderType(text, match.Index);
                if (type is null || MarkerTypes.Contains(type))
                {
                    yield return $"writes a deletion marker through {type ?? "an unknown builder"} ({Squash(match.Value)}).";
                }
            }
        }

        if (BsonDocumentMarker.Match(withStrings) is { Success: true } document)
        {
            yield return $"writes a deletion marker document ({Squash(document.Value)}).";
        }

        if (AssignedDeleted.Match(plain) is { Success: true } flag)
        {
            yield return $"assigns a deletion marker ({Squash(flag.Value)}).";
        }

        if (AssignedDeletedAt.Match(plain) is { Success: true } stamp)
        {
            yield return $"assigns a deletion marker ({Squash(stamp.Value)}).";
        }
    }

    private static string? NearestBuilderType(string text, int index)
    {
        Match? nearest = null;
        foreach (Match match in BuildersOf.Matches(text))
        {
            if (match.Index >= index)
            {
                break;
            }

            nearest = match;
        }

        var type = nearest?.Groups["type"].Value;
        return type is null ? null : type[(type.LastIndexOf('.') + 1)..];
    }

    private static string Squash(string value) => Regex.Replace(value, @"\s+", " ");

    private static IEnumerable<string> BaseClassesOfRetainedTypes(IEnumerable<string> sources)
    {
        foreach (var source in sources)
        {
            var text = Scrub(source);
            foreach (Match match in ClassHeader.Matches(text))
            {
                var end = text.IndexOfAny(['{', ';'], match.Index);
                var header = end < 0 ? text[match.Index..] : text[match.Index..end];
                if (WhereClause.Match(header) is { Success: true } where)
                {
                    header = header[..where.Index];
                }

                var colon = header.IndexOf(':', StringComparison.Ordinal);
                if (colon < 0)
                {
                    continue;
                }

                var declaration = header[..colon];
                var bases = header[(colon + 1)..];
                var typeParameters = Regex.Matches(declaration, @"<([^<>]*)>").Cast<Match>()
                    .SelectMany(generic => generic.Groups[1].Value.Split(',').Select(name => name.Trim()))
                    .Where(name => name.Length > 0).ToArray();

                var takesRetainedType = RetainedGenericArgument.IsMatch(bases);
                var openGeneric = OpenGenericBase.Matches(bases).Cast<Match>()
                    .Any(m => typeParameters.Contains(m.Groups["arg"].Value, StringComparer.Ordinal));
                if (takesRetainedType || openGeneric)
                {
                    yield return match.Groups["name"].Value;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────── the sources (the sabotage hooks live here)

    private static string FourthStoreSource()
    {
        var text = Read(RecoveryStorePath);
        return text; // SAB-FOURTH
    }

    private static IReadOnlyList<(string Path, string Source)> ScanSetSources()
    {
        var files = SourcesUnder(WorkflowFeatureRoot).Concat(SourcesUnder(InfrastructureRoot)).ToArray();
        return files; // SAB-SCANSET
    }

    private static IEnumerable<string> InfrastructureSources()
    {
        var files = SourcesUnder(InfrastructureRoot).Select(file => file.Source).ToArray();
        return files; // SAB-INFRA
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepoRoot(), relative));

    private static IEnumerable<(string Path, string Source)> SourcesUnder(string relative)
    {
        var root = RepoRoot();
        var separator = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(Path.Combine(root, relative), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                           && !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            .Select(path => (Path: Path.GetRelativePath(root, path).Replace('\\', '/'), Source: File.ReadAllText(path)))
            .ToArray();
        Assert.True(files.Length >= 10, $"only {files.Length} source files under {relative} — the scan is blind");
        return files;
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }

    // ─────────────────────────────────────────────────────────── text helpers (copies; the first guard's are private)

    private static string? ClassBody(string scrubbed, string keyword, string name)
    {
        var match = Regex.Match(scrubbed, $@"\b{keyword}\s+{Regex.Escape(name)}\b");
        if (!match.Success) return null;
        var open = scrubbed.IndexOf('{', match.Index);
        return open < 0 ? null : scrubbed[(open + 1)..ClosingIndex(scrubbed, open, '{', '}')];
    }

    private static int ClosingIndex(string text, int open, char opening, char closing)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == opening) depth++;
            else if (text[i] == closing && --depth == 0) return i;
        }

        throw new InvalidOperationException($"Unbalanced '{opening}' at {open}.");
    }

    /// <summary>
    /// Removes comments and blanks the contents of string and character literals, so neither prose nor a message can create
    /// or hide a finding, and a brace inside a literal cannot unbalance the body extraction. With
    /// <paramref name="keepStrings"/> the string literals stay as written (comments are still removed): the second read for
    /// the string-named markers.
    /// </summary>
    private static string Scrub(string source, bool keepStrings = false)
    {
        var output = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
            }
            else if (c == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 2;
            }
            else if (c == '"' || ((c == '$' || c == '@') && StartsString(source, i)))
            {
                var start = i;
                i = SkipString(source, i);
                if (keepStrings) output.Append(source, start, i - start);
                else output.Append("\"\"");
            }
            else if (c == '\'')
            {
                i = SkipCharacter(source, i);
                output.Append("''");
            }
            else
            {
                output.Append(c);
                i++;
            }
        }

        return output.ToString();
    }

    private static bool StartsString(string source, int i)
    {
        while (i < source.Length && source[i] is '$' or '@') i++;
        return i < source.Length && source[i] == '"';
    }

    private static int SkipCharacter(string source, int i)
    {
        i++;
        if (i < source.Length && source[i] == '\\') i += 2;
        else i++;
        var end = source.IndexOf('\'', Math.Min(i, source.Length - 1));
        return end < 0 ? source.Length : end + 1;
    }

    private static int SkipString(string source, int i)
    {
        var interpolated = false;
        var verbatim = false;
        while (source[i] != '"')
        {
            interpolated |= source[i] == '$';
            verbatim |= source[i] == '@';
            i++;
        }

        var quotes = 0;
        while (i + quotes < source.Length && source[i + quotes] == '"') quotes++;
        if (quotes >= 3)
        {
            var delimiter = new string('"', quotes);
            var end = source.IndexOf(delimiter, i + quotes, StringComparison.Ordinal);
            return end < 0 ? source.Length : end + quotes;
        }

        i++;
        var holes = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (holes > 0)
            {
                if (c == '"') { i = SkipString(source, i); continue; }
                if (c == '{') holes++;
                else if (c == '}') holes--;
                i++;
            }
            else if (interpolated && c == '{')
            {
                if (i + 1 < source.Length && source[i + 1] == '{') { i += 2; continue; }
                holes = 1;
                i++;
            }
            else if (!verbatim && c == '\\')
            {
                i += 2;
            }
            else if (c == '"')
            {
                if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }
                return i + 1;
            }
            else
            {
                i++;
            }
        }

        return source.Length;
    }
}
