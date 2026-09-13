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
/// SetAccountKind handler (which today parses the name and needs neither, but is the one place allowed to).</item>
/// <item>Each of the four automatic creation paths states <c>AccountKind.Unknown</c> explicitly.</item>
/// <item><c>SetAccountKind(</c> is called only from the known sites, and in the four automatic paths its argument
/// is literally <c>AccountKind.Unknown</c>.</item>
/// </list>
/// <para>K1-c sabotage: add <c>user.SetAccountKind(AccountKind.Human)</c> to RegisterCommandHandler → rules 1 and 3 go red.</para>
/// </summary>
public sealed class AccountKindCreationPathsGuardTests
{
    private const string SetHandler = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetAccountKindCommandHandler.cs";
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
    private static readonly Regex UnknownArgument = new(@"^(Diten\.AuthService\.Domain\.Enums\.)?AccountKind\.Unknown$", RegexOptions.Compiled);

    [Fact]
    public void No_production_file_but_the_SetAccountKind_handler_names_Human_or_Service()
    {
        var offenders = SourceFiles()
            .Where(f => ClassifyingLiteral.IsMatch(WithoutComments(File.ReadAllText(f.Full))))
            .Select(f => f.Relative)
            .Where(r => r != SetHandler)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "a production file classifies an account as Human/Service by literal — only the SetAccountKind handler may. "
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
        var allowedCallers = new HashSet<string>(StringComparer.Ordinal) { SetHandler, CreateHandler };
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
    public void The_scan_sees_the_production_tree()
    {
        // A scan that finds nothing is green forever and believed; pin a floor.
        var files = SourceFiles().ToArray();
        Assert.True(files.Length > 100, $"the src scan collapsed to {files.Length} files");
        Assert.Contains(files, f => f.Relative == SetHandler);
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
