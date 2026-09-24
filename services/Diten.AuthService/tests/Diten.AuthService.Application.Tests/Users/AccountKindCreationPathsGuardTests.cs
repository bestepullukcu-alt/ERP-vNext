using System.Text.RegularExpressions;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — THE SOURCE GUARD: nobody becomes Human (or Service) by structure.
///
/// <para>The owner's decision (2026-09-11) is that classification is an explicit act by a holder of the
/// explicit-grant-only key. A test that only exercises the handlers cannot see a future
/// <c>user.SetAccountKind(AccountKind.Human)</c> slipped into the register path, the invitation path, the
/// platform-admin provisioning path or the seed — so this one reads the production SOURCE (comments stripped):</para>
/// <list type="number">
/// <item>The <c>AccountKind.Human</c> / <c>AccountKind.Service</c> literals appear in NO src file except the
/// account-kind writer (which today parses the name and needs neither, but is the one place allowed to).</item>
/// <item>Each of the four automatic creation paths states <c>AccountKind.Unknown</c> explicitly.</item>
/// <item><c>SetAccountKind(</c> is called only from the known sites, and in the four automatic paths its argument
/// is literally <c>AccountKind.Unknown</c>.</item>
/// </list>
/// <para>K1-c sabotage: add <c>user.SetAccountKind(AccountKind.Human)</c> to RegisterCommandHandler → rules 1 and 3 go red.</para>
///
/// <para>WP-AUTH-USER-KIND-UPDATE-01 — an existing account's kind now has TWO doors (the account-kind endpoint and
/// the edit form's UpdateUser) and ONE writer, <c>AccountKindWriter</c>. Rule 4 counts it: the writer is the only
/// file that calls <c>SetAccountKind(</c> on an existing account (exactly once), and each door reaches it through
/// <c>_kindWriter.Apply(</c> + <c>_kindWriter.RecordAsync(</c> exactly once, never calls the entity itself and holds
/// no audit recorder of its own. Sabotage: put <c>user.SetAccountKind(newKind);</c> back into
/// UpdateUserCommandHandler → rules 3 and 4 go red.</para>
/// </summary>
public sealed class AccountKindCreationPathsGuardTests
{
    private const string SetHandler = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetAccountKindCommandHandler.cs";
    private const string UpdateHandler = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateUserCommandHandler.cs";
    private const string KindWriter = "Diten.AuthService.Application/Features/Users/Services/AccountKindWriter.cs";
    private const string CreateHandler = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs";

    private static readonly string[] AutomaticCreationPaths =
    {
        "Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs",
        "Diten.AuthService.Api/Controllers/InternalEventsController.cs",
        "Diten.AuthService.Api/Controllers/PlatformAuthController.cs",
        "Diten.AuthService.Persistence/Seed/DataSeeder.cs"
    };

    private static readonly Regex ClassifyingLiteral = new(@"\bAccountKind\.(Human|Service)\b", RegexOptions.Compiled);
    private static readonly Regex UnknownLiteral = new(@"\bAccountKind\.Unknown\b", RegexOptions.Compiled);
    private static readonly Regex SetCall = new(@"\.SetAccountKind\s*\(\s*([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex ApplyCall = new(@"\b_kindWriter\.Apply\s*\(", RegexOptions.Compiled);
    private static readonly Regex RecordCall = new(@"\b_kindWriter\.RecordAsync\s*\(", RegexOptions.Compiled);
    private static readonly Regex UnknownArgument = new(@"^(Diten\.AuthService\.Domain\.Enums\.)?AccountKind\.Unknown$", RegexOptions.Compiled);

    [Fact]
    public void No_production_file_but_the_SetAccountKind_handler_names_Human_or_Service()
    {
        var offenders = SourceFiles()
            .Where(f => ClassifyingLiteral.IsMatch(WithoutComments(File.ReadAllText(f.Full))))
            .Select(f => f.Relative)
            .Where(r => r != KindWriter)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "a production file classifies an account as Human/Service by literal — only the account-kind writer may. "
            + "Classification is an explicit, permission-gated act (owner decision 2026-09-11):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Every_automatic_creation_path_states_Unknown_explicitly()
    {
        var silent = AutomaticCreationPaths
            .Where(p => !UnknownLiteral.IsMatch(WithoutComments(File.ReadAllText(Path.Combine(SrcRoot(), p)))))
            .ToArray();

        Assert.True(silent.Length == 0,
            "these automatic creation paths do not state AccountKind.Unknown — the intent must be written, not inherited:\n"
            + string.Join("\n", silent));
    }

    [Fact]
    public void SetAccountKind_is_called_only_from_known_sites_and_automatic_paths_pass_Unknown()
    {
        var allowedCallers = new HashSet<string>(StringComparer.Ordinal) { KindWriter, CreateHandler };
        allowedCallers.UnionWith(AutomaticCreationPaths);

        var unexpected = new List<string>();
        var notUnknown = new List<string>();

        foreach (var file in SourceFiles())
        {
            var body = WithoutComments(File.ReadAllText(file.Full));
            var calls = SetCall.Matches(body);
            if (calls.Count == 0)
            {
                continue;
            }

            if (!allowedCallers.Contains(file.Relative))
            {
                unexpected.Add(file.Relative);
                continue;
            }

            if (AutomaticCreationPaths.Contains(file.Relative))
            {
                foreach (Match call in calls)
                {
                    if (!UnknownArgument.IsMatch(call.Groups[1].Value.Trim()))
                    {
                        notUnknown.Add($"{file.Relative}: SetAccountKind({call.Groups[1].Value.Trim()})");
                    }
                }
            }
        }

        Assert.True(unexpected.Count == 0, "SetAccountKind is called from a file that is not a known site:\n" + string.Join("\n", unexpected));
        Assert.True(notUnknown.Count == 0, "an automatic creation path classifies with something other than Unknown:\n" + string.Join("\n", notUnknown));
    }

    [Fact]
    public void Both_doors_write_an_existing_accounts_kind_through_the_one_writer()
    {
        var writerCalls = SetCall.Matches(WithoutComments(File.ReadAllText(Path.Combine(SrcRoot(), KindWriter)))).Count;
        Assert.True(writerCalls == 1, $"the account-kind writer must call SetAccountKind exactly once; found {writerCalls}");

        foreach (var door in new[] { SetHandler, UpdateHandler })
        {
            var body = WithoutComments(File.ReadAllText(Path.Combine(SrcRoot(), door)));
            Assert.True(SetCall.Matches(body).Count == 0,
                $"{door} writes the kind on the entity itself — a second write path beside AccountKindWriter");
            Assert.True(ApplyCall.Matches(body).Count == 1,
                $"{door} must change the kind through AccountKindWriter.Apply exactly once");
            Assert.True(RecordCall.Matches(body).Count == 1,
                $"{door} must write the kind audit row through AccountKindWriter.RecordAsync exactly once");
            Assert.False(body.Contains("IRbacAuditRecorder", StringComparison.Ordinal),
                $"{door} holds its own audit recorder — the kind row must be the writer's, not a second copy");
        }

        // No third door: the writer's Apply is reached from exactly these two handlers.
        var callers = SourceFiles()
            .Where(f => ApplyCall.IsMatch(WithoutComments(File.ReadAllText(f.Full))))
            .Select(f => f.Relative)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { SetHandler, UpdateHandler }.OrderBy(r => r, StringComparer.Ordinal).ToArray(), callers);
    }

    [Fact]
    public void The_scan_sees_the_production_tree()
    {
        // A scan that finds nothing is green forever and believed; pin a floor.
        var files = SourceFiles().ToArray();
        Assert.True(files.Length > 100, $"the src scan collapsed to {files.Length} files");
        Assert.Contains(files, f => f.Relative == SetHandler);
        Assert.Contains(files, f => f.Relative == UpdateHandler);
        Assert.Contains(files, f => f.Relative == KindWriter);
        Assert.All(AutomaticCreationPaths, p => Assert.Contains(files, f => f.Relative == p));
    }

    // ── scanning ──

    private static IEnumerable<(string Full, string Relative)> SourceFiles()
    {
        var root = SrcRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (f, Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/')));
    }

    private static string SrcRoot()
    {
        // Same probe UserLookupValidationSeedTests uses: walk up from the seeder assembly until the src tree appears.
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "services", "Diten.AuthService", "src");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            // The tests run from services/Diten.AuthService/tests/.../bin/... — src is a sibling of tests.
            var sibling = Path.Combine(directory, "src", "Diten.AuthService.Persistence", "Seed");
            if (Directory.Exists(sibling))
            {
                return Path.Combine(directory, "src");
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("services/Diten.AuthService/src was not found above the test assembly.");
    }

    private static string WithoutComments(string source)
    {
        var noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"//[^\r\n]*", string.Empty);
    }
}
