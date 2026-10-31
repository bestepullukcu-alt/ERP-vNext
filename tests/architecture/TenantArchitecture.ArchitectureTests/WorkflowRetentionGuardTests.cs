using System.Text;
using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — A WORKFLOW RECORD IS NEVER DELETED (MOD-0023 §17, AC-W1 + AC-W15, DL-1).
 *
 * WHY. Treasury's Reconcile relies on an idempotency key staying claimed forever before it re-issues with that key, so
 * WorkflowInstance, ApprovalTask and WorkflowTransitionLog are never deleted, soft-deleted or expired (§17.7/2). All three
 * repositories inherit a public RepositoryBase<T>.DeleteAsync from Platform.Common, which other modules still need, so each
 * of them overrides it to refuse.
 *
 * THE RULE (reads text, needs no Mongo). Each check is a pure function over source text, so a sabotage can be tried by
 * handing it another text, without writing to the protected file it normally reads:
 *   • the three repositories override DeleteAsync, the override only throws, and no class body deletes or writes a marker;
 *   • the three repository interfaces and IWorkflowStartRecoveryStore declare no member named Delete / Remove / Purge;
 *   • the schema manifest declares no ExpireAfter on the three collections;
 *   • Platform.Common's RepositoryBase<T>.DeleteAsync is still virtual and still writes IsDeleted + UpdatedAt;
 *   • under Infrastructure exactly the four expected classes derive from TenantRepository<T> / RepositoryBase<T> of the three
 *     types. A fifth one is red: it would inherit the delete path without anyone deciding about it.
 *
 * OPEN, BY OWNER DECISION S17-8. WorkflowStartRecoveryStore derives from TenantRepository<WorkflowInstance> and does NOT
 * override DeleteAsync (its file is outside DL-1's allowed paths). Until the next pack revision this guard holds that path
 * shut another way: its interface declares no delete member and nothing under services/Diten.Platform/src that names the
 * class calls DeleteAsync.
 *
 * AC-W1 DOES NOT FORBID AN IN-PLACE UPDATE: pseudonymising a row on an erasure request stays possible (§17.8/4).
 *
 * KNOWN WEAKNESS, STATED. This matches source text, not a syntax tree: a using alias for the base type, or a delete
 * reached through reflection, evades it. The behaviour tests (WorkflowRetentionReadMongoTests) cover the override itself.
 */
public class WorkflowRetentionGuardTests
{
    private const string RepositoriesPath =
        "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs";
    private const string InterfacesPath =
        "services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs";
    private const string RecoveryInterfacePath =
        "services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowStartRecoveryStore.cs";
    private const string ManifestPath =
        "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.WorkflowWorkCenter.cs";
    private const string CommonRepositoriesPath =
        "services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs";
    private const string InfrastructureRoot = "services/Diten.Platform/src/Diten.Platform.Infrastructure";
    private const string PlatformSourceRoot = "services/Diten.Platform/src";
    private const string RecoveryStore = "WorkflowStartRecoveryStore";

    private static readonly string[] RetainedRepositories =
        ["WorkflowInstanceRepository", "ApprovalTaskRepository", "WorkflowTransitionLogRepository"];
    private static readonly string[] RetainedInterfaces =
        ["IWorkflowInstanceRepository", "IApprovalTaskRepository", "IWorkflowTransitionLogRepository"];
    private static readonly string[] RetainedEntities = ["WorkflowInstance", "ApprovalTask", "WorkflowTransitionLog"];

    private static readonly Regex OverrideDelete = new(
        @"\boverride\s+(?:async\s+)?Task\s+DeleteAsync\s*\(", RegexOptions.Compiled);

    // After the parameter list: an expression body or a block that holds one throw statement and nothing else.
    private static readonly Regex OnlyThrows = new(
        @"^\s*(?:=>\s*throw\b[^;]*;|\{\s*throw\b[^;]*;\s*\})\s*$", RegexOptions.Compiled);

    private static readonly Regex DeletingCall = new(
        @"\b(?:DeleteOne|DeleteMany|FindOneAndDelete)(?:Async)?\b|\bbase\s*\.\s*DeleteAsync\b", RegexOptions.Compiled);

    private static readonly Regex MarkerWrite = new(
        @"\.\s*Set\s*\(\s*\w+\s*=>\s*\w+\s*\.\s*(?:IsDeleted|DeletedAt)\b|\b(?:IsDeleted|DeletedAt)\s*=(?!=)", RegexOptions.Compiled);

    // Any member: a method (with or without type arguments) or a property. A call inside a default body counts too:
    // a false positive is red, never a pass.
    private static readonly Regex MemberName = new(
        @"\b(?<name>\w+)\s*(?:<[^<>()]*(?:<[^<>()]*>[^<>()]*)*>)?\s*(?:\(|\{\s*(?:get|set|init)\b)", RegexOptions.Compiled);

    private static readonly Regex DeletionWord = new(
        "delete|remove|purge", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DerivingClass = new(
        @"\bclass\s+(?<name>\w+)[^{;]*?:[^{;]*?\b(?:TenantRepository|RepositoryBase)\s*<\s*(?:[\w.]+\.)?(?:WorkflowInstance|ApprovalTask|WorkflowTransitionLog)\s*>",
        RegexOptions.Compiled);

    private static readonly Regex DeleteAsyncCall = new(@"\bDeleteAsync\s*\(", RegexOptions.Compiled);

    // ─────────────────────────────────────────────────────────── the rules, over the real sources

    [Fact]
    public void AC_W1_the_three_repositories_override_DeleteAsync_and_the_override_only_throws()
    {
        var findings = RetainedRepositories.SelectMany(name => RepositoryFindings(RepositoriesSource(), name)).ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AC_W1_the_repository_interfaces_declare_no_delete_member()
    {
        var findings = RetainedInterfaces.SelectMany(name => InterfaceFindings(InterfacesSource(), name)).ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AC_W1_the_schema_manifest_declares_no_ExpireAfter_on_the_three_collections()
    {
        var findings = RetainedEntities.SelectMany(entity => ManifestFindings(ManifestSource(), entity)).ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AC_W15_Platform_Common_base_delete_is_still_virtual_and_still_writes_IsDeleted_and_UpdatedAt()
    {
        var findings = CommonFindings(Read(CommonRepositoriesPath)).ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AC_W1_exactly_the_expected_classes_derive_from_the_repository_bases_of_the_three_types()
    {
        var found = DerivingClasses(InfrastructureSources()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(RetainedRepositories.Append(RecoveryStore).Order(StringComparer.Ordinal), found);
    }

    [Fact]
    public void AC_W1_WorkflowStartRecoveryStore_has_no_delete_member_and_no_DeleteAsync_call()
    {
        var findings = InterfaceFindings(Read(RecoveryInterfacePath), "I" + RecoveryStore)
            .Concat(RecoveryCallFindings(SourcesUnder(PlatformSourceRoot)))
            .ToArray();
        Assert.True(findings.Length == 0, string.Join("\n", findings));
    }

    // ─────────────────────────────────────────────────────────── the rules have teeth (a text, never a protected file)

    private const string RefusingRepository = """
        public sealed class ApprovalTaskRepository : TenantRepository<ApprovalTask>, IApprovalTaskRepository
        {
            // Comments do not count: IsDeleted = true; Collection.DeleteOneAsync(x);
            public override Task DeleteAsync(Guid id, CancellationToken ct = default) =>
                throw new NotSupportedException("Retained; { IsDeleted = true }.");
        }
        """;

    [Fact]
    public void A_refusing_override_passes() =>
        Assert.Empty(RepositoryFindings(RefusingRepository, "ApprovalTaskRepository"));

    [Theory]
    [InlineData("public override Task DeleteAsync(Guid id, CancellationToken ct = default) => base.DeleteAsync(id, ct);")]
    [InlineData("public override Task DeleteAsync(Guid id, CancellationToken ct = default) => Collection.DeleteOneAsync(x => x.Id == id, ct);")]
    [InlineData("public override async Task DeleteAsync(Guid id, CancellationToken ct = default) { await Collection.UpdateOneAsync(f, Builders<ApprovalTask>.Update.Set(x => x.IsDeleted, true)); }")]
    [InlineData("public override Task DeleteAsync(Guid id, CancellationToken ct = default) { Touch(); throw new NotSupportedException(); }")]
    [InlineData("public Task OtherAsync() => Task.CompletedTask;")]
    public void An_override_that_writes_or_is_missing_is_red(string member) =>
        Assert.NotEmpty(RepositoryFindings(
            "public sealed class ApprovalTaskRepository : TenantRepository<ApprovalTask> { " + member + " }",
            "ApprovalTaskRepository"));

    [Theory]
    [InlineData("public async Task CloseAsync() { await Collection.UpdateOneAsync(f, Builders<ApprovalTask>.Update.Set(x => x.DeletedAt, now)); }")]
    [InlineData("public void Mark(ApprovalTask task) { task.IsDeleted = true; }")]
    [InlineData("public Task WipeAsync() => Collection.DeleteManyAsync(f);")]
    public void A_write_elsewhere_in_the_class_body_is_red(string member) =>
        Assert.NotEmpty(RepositoryFindings(
            RefusingRepository.Replace("// Comments do not count", member + "\n// Comments do not count", StringComparison.Ordinal),
            "ApprovalTaskRepository"));

    [Fact]
    public void A_filter_on_IsDeleted_is_a_read_not_a_write() =>
        Assert.Empty(RepositoryFindings(
            RefusingRepository.Replace("// Comments do not count", "public bool Live(ApprovalTask t) => t.IsDeleted == false;\n// Comments do not count", StringComparison.Ordinal),
            "ApprovalTaskRepository"));

    [Theory]
    [InlineData("Task DeleteAsync(Guid id, CancellationToken ct = default);")]
    [InlineData("Task<bool> SoftDeleteAsync(Guid id);")]
    [InlineData("Task REMOVEAsync<T>(Guid id);")]
    [InlineData("Task PurgeExpiredAsync(CancellationToken ct = default) => throw new NotSupportedException();")]
    [InlineData("Func<Guid, Task> Deleter { get; }")]
    public void A_delete_member_on_an_interface_is_red(string member) =>
        Assert.NotEmpty(InterfaceFindings("public interface IApprovalTaskRepository { " + member + " }", "IApprovalTaskRepository"));

    [Fact]
    public void A_comment_on_an_interface_is_not_a_member() =>
        Assert.Empty(InterfaceFindings(
            "public interface IApprovalTaskRepository { /* DeleteAsync(id) */ // Remove(x)\n Task<T> ReadAsync(Guid id); }",
            "IApprovalTaskRepository"));

    [Fact]
    public void An_ExpireAfter_in_the_collections_block_is_red_and_one_elsewhere_is_not()
    {
        const string manifest = """
            Collection<ApprovalTask>(SchemaProfile.WorkflowWorkCenter, Name, new CreateIndexOptions { Name = "x" }),
            Collection<TaskItem>(SchemaProfile.WorkflowWorkCenter, Name, new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(1) })
            """;
        Assert.Empty(ManifestFindings(manifest, "ApprovalTask"));
        Assert.NotEmpty(ManifestFindings(manifest.Replace("Name = \"x\"", "ExpireAfter = TimeSpan.FromDays(1)", StringComparison.Ordinal), "ApprovalTask"));
        Assert.NotEmpty(ManifestFindings(manifest, "WorkflowInstance")); // a collection the text does not hold: the scan is blind, red
    }

    [Theory]
    [InlineData("public sealed class Extra : TenantRepository<ApprovalTask>, IExtra { }", "Extra")]
    [InlineData("public sealed class Extra(IPlatformDbContext db)\n    : RepositoryBase<Diten.Platform.Domain.Entities.Workflow.WorkflowTransitionLog> { }", "Extra")]
    public void A_class_deriving_a_retained_type_is_listed(string source, string name) =>
        Assert.Equal([name], DerivingClasses([source]));

    [Fact]
    public void A_class_deriving_another_type_or_a_commented_class_is_not_listed() =>
        Assert.Empty(DerivingClasses([
            "public sealed class SlaEscalationRuleRepository : TenantRepository<SlaEscalationRule> { }",
            "// public sealed class Extra : TenantRepository<ApprovalTask> { }"
        ]));

    [Fact]
    public void A_DeleteAsync_call_beside_the_recovery_store_is_red()
    {
        Assert.NotEmpty(RecoveryCallFindings([("a.cs", "class A { IWorkflowStartRecoveryStore s; Task B() => s.DeleteAsync(id); }")]));
        Assert.Empty(RecoveryCallFindings([("a.cs", "class A { IWorkflowStartRecoveryStore s; }"), ("b.cs", "class B { Task C() => r.DeleteAsync(id); }")]));
    }

    [Theory]
    [InlineData("public async Task DeleteAsync(Guid id) { Update.Set(e => e.IsDeleted, true).Set(e => e.UpdatedAt, now); }")]
    [InlineData("public virtual async Task DeleteAsync(Guid id) { Update.Set(e => e.IsDeleted, true); }")]
    [InlineData("public virtual async Task DeleteAsync(Guid id) { Update.Set(e => e.UpdatedAt, now); }")]
    public void A_changed_common_base_delete_is_red(string member) =>
        Assert.NotEmpty(CommonFindings("public abstract class RepositoryBase<TEntity> { " + member + " }"));

    // ─────────────────────────────────────────────────────────── the checks (pure: text in, findings out)

    private static IEnumerable<string> RepositoryFindings(string source, string className)
    {
        var body = ClassBody(Scrub(source), "class", className);
        if (body is null)
        {
            yield return $"{className}: class not found — the scan is blind.";
            yield break;
        }

        var match = OverrideDelete.Match(body);
        if (!match.Success)
        {
            yield return $"{className}: no override of DeleteAsync — it inherits RepositoryBase<T>'s soft delete.";
        }
        else
        {
            var parametersEnd = ClosingIndex(body, match.Index + match.Length - 1, '(', ')');
            var rest = body[(parametersEnd + 1)..];
            var memberEnd = MemberEnd(rest);
            var member = rest[..memberEnd];
            if (!OnlyThrows.IsMatch(member))
            {
                yield return $"{className}: the DeleteAsync override must only throw, found: {member.Trim()}";
            }
        }

        if (DeletingCall.Match(body) is { Success: true } call)
        {
            yield return $"{className}: deletes rows ({call.Value}).";
        }

        if (MarkerWrite.Match(body) is { Success: true } write)
        {
            yield return $"{className}: writes a deletion marker ({write.Value}).";
        }
    }

    private static IEnumerable<string> InterfaceFindings(string source, string interfaceName)
    {
        var body = ClassBody(Scrub(source), "interface", interfaceName);
        if (body is null)
        {
            return [$"{interfaceName}: interface not found — the scan is blind."];
        }

        return MemberName.Matches(body)
            .Select(match => match.Groups["name"].Value)
            .Where(name => DeletionWord.IsMatch(name))
            .Distinct(StringComparer.Ordinal)
            .Select(name => $"{interfaceName}: a member named {name} — these records are never deleted.")
            .ToArray();
    }

    private static IEnumerable<string> ManifestFindings(string source, string entity)
    {
        var scrubbed = Scrub(source);
        var match = Regex.Match(scrubbed, $@"\bCollection<{entity}>\s*\(");
        if (!match.Success)
        {
            return [$"{entity}: no Collection<{entity}>(…) block in the manifest — the scan is blind."];
        }

        var open = match.Index + match.Length - 1;
        var block = scrubbed[open..(ClosingIndex(scrubbed, open, '(', ')') + 1)];
        return block.Contains("ExpireAfter", StringComparison.Ordinal)
            ? [$"{entity}: the manifest declares ExpireAfter (TTL) on a collection that is retained forever."]
            : [];
    }

    private static IEnumerable<string> CommonFindings(string source)
    {
        var body = ClassBody(Scrub(source), "class", "RepositoryBase");
        if (body is null)
        {
            yield return "RepositoryBase<T>: class not found — the scan is blind.";
            yield break;
        }

        var match = Regex.Match(body, @"\bpublic\s+virtual\s+async\s+Task\s+DeleteAsync\s*\(");
        if (!match.Success)
        {
            yield return "RepositoryBase<T>.DeleteAsync is no longer `public virtual async Task`: other modules depend on it.";
            yield break;
        }

        var member = body[match.Index..];
        var open = member.IndexOf('{', StringComparison.Ordinal);
        member = member[open..(ClosingIndex(member, open, '{', '}') + 1)];
        foreach (var field in new[] { "IsDeleted", "UpdatedAt" })
        {
            if (!Regex.IsMatch(member, $@"\.\s*Set\s*\(\s*\w+\s*=>\s*\w+\s*\.\s*{field}\b"))
            {
                yield return $"RepositoryBase<T>.DeleteAsync no longer writes {field}: Platform.Common must stay unchanged.";
            }
        }
    }

    private static IReadOnlyList<string> DerivingClasses(IEnumerable<string> sources) =>
        sources.SelectMany(source => DerivingClass.Matches(Scrub(source)).Select(match => match.Groups["name"].Value)).ToArray();

    private static IEnumerable<string> RecoveryCallFindings(IEnumerable<(string Path, string Source)> sources) =>
        sources
            .Select(file => (file.Path, Text: Scrub(file.Source)))
            .Where(file => file.Text.Contains(RecoveryStore, StringComparison.Ordinal) && DeleteAsyncCall.IsMatch(file.Text))
            .Select(file => $"{file.Path}: names {RecoveryStore} and calls DeleteAsync — that store keeps the inherited delete (S17-8), nothing may call it.");

    // ─────────────────────────────────────────────────────────── the sources

    private static string RepositoriesSource() => Read(RepositoriesPath);

    private static string InterfacesSource() => Read(InterfacesPath);

    private static string ManifestSource() => Read(ManifestPath);

    private static IEnumerable<string> InfrastructureSources() => SourcesUnder(InfrastructureRoot).Select(file => file.Source);

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

    // ─────────────────────────────────────────────────────────── text helpers

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

    // The end (exclusive) of the member whose parameter list has just closed: `=> …;` or a balanced `{ … }`.
    private static int MemberEnd(string rest)
    {
        for (var i = 0; i < rest.Length; i++)
        {
            if (rest[i] == ';') return i + 1;
            if (rest[i] == '{') return ClosingIndex(rest, i, '{', '}') + 1;
        }

        return rest.Length;
    }

    /// <summary>
    /// Removes comments and blanks the contents of string and character literals, so neither prose nor a message can create
    /// or hide a finding, and a brace inside a literal cannot unbalance the body extraction.
    /// </summary>
    private static string Scrub(string source)
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
                i = SkipString(source, i);
                output.Append("\"\"");
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
